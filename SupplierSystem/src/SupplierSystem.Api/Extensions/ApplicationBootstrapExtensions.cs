using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Authorization;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Middleware;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Audit;
using SupplierSystem.Api.Services.Auth;
using SupplierSystem.Api.Services.ChangeRequests;
using SupplierSystem.Api.Services.Compliance;
using SupplierSystem.Api.Services.Contracts;
using SupplierSystem.Api.Services.Files;
using SupplierSystem.Api.Services.FileUploads;
using SupplierSystem.Api.Services.Invoices;
using SupplierSystem.Api.Services.ItemMaster;
using SupplierSystem.Api.Services.Notifications;
using SupplierSystem.Api.Services.Requisitions;
using SupplierSystem.Api.Services.Reminders;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Api.Services.Reconciliation;
using SupplierSystem.Api.Services.Scheduling;
using SupplierSystem.Api.Services.Settlements;
using SupplierSystem.Api.Services.Startup;
using SupplierSystem.Api.Services.Templates;
using SupplierSystem.Api.Services.WarehouseReceipts;
using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Repositories;
using SupplierSystem.Infrastructure.Services;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace SupplierSystem.Api.Extensions;

public static class ApplicationBootstrapExtensions
{
    public static void ConfigureApiHost(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
        });
    }

    public static void AddSupplierSystemApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<GlobalExceptionFilter>();
                options.Filters.Add<ApiResponseFilter>();
            })
            .AddJsonOptions(options => { options.JsonSerializerOptions.MaxDepth = 64; });

        builder.Services.AddOpenApi();
        builder.Services.AddSupplierAuthorization();
        builder.Services.AddSupplierCors(builder.Configuration);

        var connectionString = builder.Configuration.GetConnectionString("SupplierSystem");
        builder.Services.AddDbContext<SupplierSystemDbContext>(options => options.UseSqlServer(connectionString));

        builder.Services.AddMemoryCache();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient();

        builder.Services.Configure<MaintenanceSchedulerOptions>(builder.Configuration.GetSection("Scheduler"));
        builder.Services.AddJwtAuthentication(builder.Configuration);

        builder.Services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
        builder.Services.AddScoped<IAuthPayloadService, AuthPayloadService>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<IAuditArchiveService, AuditArchiveService>();
        builder.Services.AddScoped<IAccountLockoutService, AccountLockoutService>();

        var disableLoginLock = builder.Configuration.GetValue<bool>("DisableLoginLock") ||
            string.Equals(Environment.GetEnvironmentVariable("DISABLE_LOGIN_LOCK"), "1", StringComparison.OrdinalIgnoreCase);
        if (disableLoginLock)
        {
            builder.Services.AddScoped<ILoginLockService, NoopLoginLockService>();
        }
        else
        {
            builder.Services.AddScoped<ILoginLockService, LoginLockService>();
        }

        builder.Services.AddScoped<ISessionService, SessionService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IAuthRepository, AuthRepository>();
        builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
        builder.Services.AddSingleton<LoginRateLimitService>();
        builder.Services.AddScoped<ISupplierService, SupplierService>();
        builder.Services.AddScoped<PrExcelService>();
        builder.Services.AddSingleton<SupplierSystem.Api.Services.Excel.ExcelOpenXmlReader>();
        builder.Services.AddScoped<SupplierSystem.Api.Services.Rfq.RfqExcelImportService>();
        builder.Services.AddScoped<ItemMasterImportService>();
        builder.Services.AddScoped<RfqStateMachine>();
        builder.Services.AddScoped<QuoteStateMachine>();
        builder.Services.AddScoped<ReconciliationStateMachine>();
        builder.Services.AddScoped<RfqService>();
        builder.Services.AddScoped<RfqQuoteService>();
        builder.Services.AddScoped<RfqWorkflowStore>();
        builder.Services.AddScoped<RfqComparisonPrintService>();
        builder.Services.AddScoped<ReconciliationStore>();
        builder.Services.AddScoped<RfqPriceAuditService>();
        builder.Services.AddScoped<LineItemWorkflowService>();
        builder.Services.AddScoped<PurchaseOrderService>();
        builder.Services.AddScoped<PublicRfqService>();
        builder.Services.AddScoped<SupplierSystem.Api.Services.Registrations.SupplierRegistrationService>();
        builder.Services.AddScoped<SystemLockdownService>();
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddScoped<DashboardDataService>();
        builder.Services.AddScoped<WhitelistBlacklistStore>();
        builder.Services.AddScoped<ContractStore>();
        builder.Services.AddScoped<NotificationStore>();
        builder.Services.AddScoped<WarehouseReceiptStore>();
        builder.Services.AddScoped<EmailSettingsStore>();
        builder.Services.AddScoped<UserDirectoryService>();
        builder.Services.AddScoped<ExchangeRateHistoryService>();
        builder.Services.AddScoped<PurchasingGroupDataService>();
        builder.Services.AddScoped<OrganizationalUnitDataService>();
        builder.Services.AddScoped<CountryFreightRateDataService>();
        builder.Services.AddScoped<PermissionsDataService>();
        builder.Services.AddScoped<BuyerAssignmentDataService>();
        builder.Services.AddScoped<RfqControllerDataService>();
        builder.Services.AddScoped<FileAccessTokenStore>();
        builder.Services.AddScoped<TemplateDocumentStore>();
        builder.Services.AddScoped<InvoiceStore>();
        builder.Services.AddScoped<SettlementStore>();
        builder.Services.AddScoped<AuditReadStore>();
        builder.Services.AddScoped<AuthReadStore>();
        builder.Services.AddScoped<RequisitionStore>();
        builder.Services.AddScoped<SystemHealthService>();
        builder.Services.AddScoped<LegacyContractDeprecationFilter>();
        builder.Services.AddScoped<BackupService>();
        builder.Services.AddScoped<BackupAlertService>();
        builder.Services.AddScoped<ArchiveService>();
        builder.Services.AddScoped<BackupScheduler>();
        builder.Services.AddScoped<ReminderNotifier>();
        builder.Services.AddScoped<ReminderQueueService>();
        builder.Services.AddHostedService<MaintenanceScheduler>();
        builder.Services.AddScoped<ExchangeRateService>();
        builder.Services.AddScoped<TariffCalculationService>();
        builder.Services.AddSingleton<AuthSchemaMonitor>();
        builder.Services.AddScoped<CompatibilitySchemaService>();
        builder.Services.AddScoped<CompatibilityMigrationService>();
        builder.Services.AddScoped<ChangeRequestRepository>();
        builder.Services.AddScoped<ChangeRequestService>();
        builder.Services.AddScoped<WorkflowStore>();
        builder.Services.AddScoped<WorkflowEngine>();
        builder.Services.AddScoped<FileUploadRepository>();
        builder.Services.AddScoped<FileUploadService>();
        builder.Services.AddScoped<FileUploadReminderService>();
        builder.Services.AddScoped<SupplierSystem.Api.Services.Files.SupplierFileRepository>();
        builder.Services.AddScoped<AdminBootstrapStartupGuard>();
    }

    public static async Task InitializeSupplierSystemAsync(this WebApplication app)
    {
        app.RegisterRuntimeDiagnostics();

        var connectionString = app.Configuration.GetConnectionString("SupplierSystem");
        LogConnectionString(app, connectionString);

        using var scope = app.Services.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<AdminBootstrapStartupGuard>();
        await guard.EnsureInitializedAsync(app.Lifetime.ApplicationStopping);
    }

    public static void UseSupplierSystemApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseHttpsRedirection();
        }

        app.UseCors("default");
        app.UseRequestDiagnostics();
        app.UseMiddleware<RfqLegacyContractRoutingMiddleware>();

        var webRootPath = app.Environment.WebRootPath
            ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var spaIndexPath = Path.Combine(webRootPath, "index.html");
        var serveSpa = File.Exists(spaIndexPath);

        if (serveSpa)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.Logger.LogInformation(
            "SPA static root: {WebRootPath}. Index: {IndexPath}. Exists: {Exists}.",
            webRootPath,
            spaIndexPath,
            serveSpa);

        app.UseMiddleware<AccountLockoutMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<SupplierPasswordChangeEnforcementMiddleware>();
        app.UseAuthorization();

        var uploadsPath = app.Configuration["Uploads:Path"];
        if (string.IsNullOrWhiteSpace(uploadsPath))
        {
            uploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        }
        if (string.IsNullOrWhiteSpace(uploadsPath))
        {
            uploadsPath = Environment.GetEnvironmentVariable("UPLOAD_DIR");
        }
        if (string.IsNullOrWhiteSpace(uploadsPath))
        {
            uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UPLOADS_PATH")))
        {
            Environment.SetEnvironmentVariable("UPLOADS_PATH", uploadsPath);
        }
        try
        {
            Directory.CreateDirectory(uploadsPath);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to initialize uploads directory at {UploadsPath}.", uploadsPath);
        }

        app.MapControllers();

        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/__spa_debug", () => Results.Json(new
            {
                contentRoot = app.Environment.ContentRootPath,
                webRootPath,
                spaIndexPath,
                indexExists = File.Exists(spaIndexPath),
                serveSpa,
            }));
        }

        if (serveSpa)
        {
            app.MapFallbackToFile("index.html");
        }
    }

    private static void AddSupplierAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.OrgUnitsManage,
                policy => policy.Requirements.Add(new PermissionRequirement(
                    new[] { Permissions.AdminOrgUnitsManage },
                    allowAdminRole: true)));

            options.AddPolicy(
                AuthorizationPolicies.BuyerAssignmentsAdmin,
                policy => policy.Requirements.Add(new PermissionRequirement(
                    new[] { Permissions.AdminBuyerAssignmentsManage },
                    allowAdminRole: false)));

            options.AddPolicy(
                AuthorizationPolicies.BuyerAssignmentsRead,
                policy => policy.Requirements.Add(new BuyerAssignmentsReadRequirement()));

            var permissionPolicies = typeof(Permissions)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(string))
                .Select(field => field.GetValue(null) as string)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var permission in permissionPolicies)
            {
                options.AddPolicy(
                    permission!,
                    policy => policy.Requirements.Add(new PermissionRequirement(
                        new[] { permission! })));
            }
        });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, BuyerAssignmentsReadHandler>();
    }

    private static void AddSupplierCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsOrigins = configuration["Cors:AllowedOrigins"]
            ?? Environment.GetEnvironmentVariable("CORS_ORIGINS")
            ?? Environment.GetEnvironmentVariable("CLIENT_ORIGIN");
        var allowedOrigins = string.IsNullOrWhiteSpace(corsOrigins)
            ? new[] { "http://localhost", "http://localhost:5173", "http://127.0.0.1:5173" }
            : corsOrigins
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(origin => origin.Trim())
                .Where(origin => origin.Length > 0)
                .ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy("default", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }

    private static void LogConnectionString(WebApplication app, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            app.Logger.LogError(
                "Startup config missing: ConnectionStrings:SupplierSystem is empty. ContentRoot: {ContentRoot}. Environment: {Environment}.",
                app.Environment.ContentRootPath,
                app.Environment.EnvironmentName);
            return;
        }

        app.Logger.LogInformation(
            "Startup config: ConnectionStrings:SupplierSystem resolved ({ConnectionString}). ContentRoot: {ContentRoot}. Environment: {Environment}.",
            MaskConnectionString(connectionString),
            app.Environment.ContentRootPath,
            app.Environment.EnvironmentName);
    }

    private static void RegisterRuntimeDiagnostics(this WebApplication app)
    {
        if (!IsRuntimeDiagnosticsEnabled(app.Configuration))
        {
            return;
        }

        var logger = app.Logger;
        var environment = app.Environment.EnvironmentName;
        var contentRoot = app.Environment.ContentRootPath;

        void SafeLog(Action logAction)
        {
            try
            {
                logAction();
            }
            catch
            {
                // Avoid crashing on logging during shutdown or provider disposal.
            }
        }

        SafeLog(() => logger.LogWarning(
            "Runtime diagnostics enabled. Environment: {Environment}. ContentRoot: {ContentRoot}.",
            environment,
            contentRoot));

        app.Lifetime.ApplicationStarted.Register(() =>
            SafeLog(() => logger.LogInformation("[Diag] Lifetime event: ApplicationStarted.")));
        app.Lifetime.ApplicationStopping.Register(() =>
            SafeLog(() => logger.LogWarning("[Diag] Lifetime event: ApplicationStopping.")));
        app.Lifetime.ApplicationStopped.Register(() =>
            SafeLog(() => logger.LogWarning("[Diag] Lifetime event: ApplicationStopped.")));

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                SafeLog(() => logger.LogCritical(exception, "[Diag] AppDomain unhandled exception. IsTerminating: {IsTerminating}", args.IsTerminating));
            }
            else
            {
                SafeLog(() => logger.LogCritical("[Diag] AppDomain unhandled exception object: {ExceptionObject}. IsTerminating: {IsTerminating}", args.ExceptionObject, args.IsTerminating));
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            SafeLog(() => logger.LogError(args.Exception, "[Diag] Unobserved task exception."));
        };

        AssemblyLoadContext.Default.Unloading += _ =>
        {
            SafeLog(() => logger.LogWarning("[Diag] AssemblyLoadContext.Default.Unloading triggered."));
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            SafeLog(() => logger.LogWarning("[Diag] AppDomain.ProcessExit triggered."));
        };
    }

    private static void UseRequestDiagnostics(this WebApplication app)
    {
        if (!IsRuntimeDiagnosticsEnabled(app.Configuration))
        {
            return;
        }

        app.Use(async (context, next) =>
        {
            if (!ShouldLogRequest(context.Request.Path))
            {
                await next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            app.Logger.LogInformation(
                "[Diag] HTTP {Method} {Path}{QueryString} started.",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty);

            try
            {
                await next(context);
                stopwatch.Stop();

                app.Logger.LogInformation(
                    "[Diag] HTTP {Method} {Path} completed {StatusCode} in {ElapsedMs} ms.",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                app.Logger.LogError(
                    ex,
                    "[Diag] HTTP {Method} {Path} failed after {ElapsedMs} ms.",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        });
    }

    private static bool IsRuntimeDiagnosticsEnabled(IConfiguration configuration)
    {
        var configured = configuration.GetValue<bool?>("Diagnostics:RuntimeLifecycleLogging");
        if (configured.HasValue)
        {
            return configured.Value;
        }

        var envValue = Environment.GetEnvironmentVariable("DIAGNOSTICS_RUNTIME_LIFECYCLE_LOGGING");
        return TryParseBooleanSwitch(envValue, out var parsed) && parsed;
    }

    private static bool TryParseBooleanSwitch(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "1", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(trimmed, "0", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        return bool.TryParse(trimmed, out result);
    }

    private static bool ShouldLogRequest(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(value);
        return string.IsNullOrWhiteSpace(extension);
    }

    private static string MaskConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = part[..separatorIndex].Trim();
            if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Pwd", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = $"{key}=*****";
            }
        }

        return string.Join(';', parts) + ";";
    }
}
