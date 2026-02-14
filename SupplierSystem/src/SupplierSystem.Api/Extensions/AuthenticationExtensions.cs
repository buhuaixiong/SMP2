using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Extensions
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var secret = configuration["JwtSettings:Secret"];
            if (string.IsNullOrWhiteSpace(secret) || IsPlaceholderSecret(secret))
            {
                secret = Environment.GetEnvironmentVariable("JWT_SECRET");
            }

            if (string.IsNullOrWhiteSpace(secret) || secret.Trim().Length < 32)
            {
                Console.Error.WriteLine("[SECURITY] CRITICAL: JWT_SECRET must be configured and at least 32 characters long.");
                throw new InvalidOperationException("JWT_SECRET is missing or too short.");
            }

            secret = secret.Trim();
            var issuer = configuration["JwtSettings:Issuer"] ?? "supplier-system";
            var audience = configuration["JwtSettings:Audience"] ?? "supplier-system";

            var primaryKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            // Optional Node token support (compatibility during migration).
            var nodeSecret = configuration["NodeJwt:Secret"];
            if (string.IsNullOrWhiteSpace(nodeSecret) || IsPlaceholderSecret(nodeSecret))
            {
                nodeSecret = Environment.GetEnvironmentVariable("NODE_JWT_SECRET");
            }

            var signingKeys = new List<SecurityKey> { primaryKey };
            if (!string.IsNullOrWhiteSpace(nodeSecret))
            {
                nodeSecret = nodeSecret.Trim();
                if (nodeSecret.Length >= 32 && !nodeSecret.Equals(secret, StringComparison.Ordinal))
                {
                    signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(nodeSecret)));
                }
            }

            var allowQueryToken =
                configuration.GetValue("Auth:AllowQueryToken", false) ||
                string.Equals(Environment.GetEnvironmentVariable("AUTH_ALLOW_QUERY_TOKEN"), "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Environment.GetEnvironmentVariable("AUTH_ALLOW_QUERY_TOKEN"), "true", StringComparison.OrdinalIgnoreCase);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = primaryKey,
                    IssuerSigningKeyResolver = (token, securityToken, kid, parameters) => signingKeys,
                    RoleClaimType = "role",
                    IssuerValidator = (iss, token, parameters) =>
                    {
                        if (string.IsNullOrWhiteSpace(iss))
                        {
                            // Node tokens do not emit iss; accept and normalize.
                            return issuer;
                        }

                        if (string.Equals(iss, issuer, StringComparison.Ordinal))
                        {
                            return iss;
                        }

                        throw new SecurityTokenInvalidIssuerException("Invalid issuer.");
                    },
                    AudienceValidator = (audiences, token, parameters) =>
                    {
                        var hasAny = false;
                        if (audiences != null)
                        {
                            foreach (var aud in audiences)
                            {
                                hasAny = true;
                                if (!string.IsNullOrWhiteSpace(aud) &&
                                    string.Equals(aud, audience, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }

                        // No audience claim -> allow (Node token compatibility).
                        return !hasAny;
                    },
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var header = context.Request.Headers.Authorization.ToString();
                        var token = ExtractBearerToken(header);
                        if (string.IsNullOrWhiteSpace(token) && allowQueryToken)
                        {
                            token = context.Request.Query["token"].ToString();
                        }

                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            context.Token = token;
                            context.HttpContext.Items["AuthToken"] = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var tokenBlacklist = context.HttpContext.RequestServices
                            .GetRequiredService<ITokenBlacklistService>();
                        var payloadService = context.HttpContext.RequestServices
                            .GetRequiredService<IAuthPayloadService>();

                        var token = context.HttpContext.Items["AuthToken"] as string;
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            token = ExtractBearerToken(context.Request.Headers.Authorization.ToString());
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            var reason = await tokenBlacklist.GetReasonAsync(token);
                            if (!string.IsNullOrWhiteSpace(reason))
                            {
                                context.HttpContext.Items["AuthBlacklistReason"] = reason;
                                context.Fail("Token has been revoked");
                                return;
                            }
                        }

                        var sub = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                  ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                        if (string.IsNullOrWhiteSpace(sub))
                        {
                            context.Fail("Token subject missing");
                            return;
                        }

                        var enforceAuthVersion = ShouldEnforceAuthVersion(context.HttpContext);
                        var authVersionClaim = context.Principal?.FindFirst("authVersion")?.Value;
                        var tokenAuthVersion = TryParseAuthVersion(authVersionClaim);

                        if (!tokenAuthVersion.HasValue && enforceAuthVersion)
                        {
                            context.Fail("Token version missing");
                            return;
                        }

                        if (tokenAuthVersion.HasValue)
                        {
                            var dbContext = context.HttpContext.RequestServices
                                .GetRequiredService<SupplierSystemDbContext>();

                            var currentVersion = await dbContext.Users
                                .AsNoTracking()
                                .Where(u => u.Id == sub)
                                .Select(u => (int?)u.AuthVersion)
                                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                            if (!currentVersion.HasValue)
                            {
                                context.Fail("User not found");
                                return;
                            }

                            if (currentVersion.Value != tokenAuthVersion.Value)
                            {
                                context.Fail("Token has been superseded");
                                return;
                            }
                        }

                        var user = await payloadService.BuildAsync(sub);
                        if (user == null)
                        {
                            context.Fail("User not found");
                            return;
                        }

                        context.HttpContext.Items["AuthUser"] = user;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var request = context.HttpContext.Request;
                        var response = context.HttpContext.Response;

                        var headerToken = ExtractBearerToken(request.Headers.Authorization.ToString());
                        var queryToken = allowQueryToken ? request.Query["token"].ToString() : null;
                        var hasAnyToken = !string.IsNullOrWhiteSpace(headerToken) || !string.IsNullOrWhiteSpace(queryToken);

                        var blacklistReason = context.HttpContext.Items["AuthBlacklistReason"] as string;

                        response.StatusCode = StatusCodes.Status401Unauthorized;
                        response.ContentType = "application/json";

                        object body;
                        if (!hasAnyToken)
                        {
                            body = new { message = "Authentication token is missing." };
                        }
                        else if (string.Equals(blacklistReason, "superseded", StringComparison.OrdinalIgnoreCase))
                        {
                            body = new
                            {
                                message = "Your session has been ended because you logged in from another device.",
                                code = "SESSION_SUPERSEDED",
                            };
                        }
                        else if (!string.IsNullOrWhiteSpace(blacklistReason))
                        {
                            body = new { message = "Token has been revoked. Please login again." };
                        }
                        else
                        {
                            body = new { message = "Invalid or expired authentication token." };
                        }

                        await response.WriteAsync(JsonSerializer.Serialize(body));
                    }
                };
            });

            return services;
        }

        private static bool IsPlaceholderSecret(string secret)
        {
            var trimmed = secret.Trim();
            if (trimmed.Equals("${JWT_SECRET}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmed.StartsWith("${", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static int? TryParseAuthVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static bool ShouldEnforceAuthVersion(HttpContext context)
        {
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            if (configuration.GetValue("Auth:EnforceTokenVersion", true))
            {
                return true;
            }

            var env = Environment.GetEnvironmentVariable("AUTH_VERSION_ENFORCE");
            return string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(env, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ExtractBearerToken(string? header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return null;
            }

            var value = header.Trim();
            if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = value.Substring("Bearer ".Length).Trim();
            return token.Length > 0 ? token : null;
        }
    }
}
