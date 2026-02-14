using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SupplierSystem.Tests.Helpers;
using Xunit;

namespace SupplierSystem.Tests.Integration;

public sealed class ApiContractTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly Regex MethodRegex = new(
        @"method\s*:\s*[""'](?<method>[A-Za-z]+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PathParameterRegex = new(@"\{[^}/]+\}", RegexOptions.Compiled);
    private static readonly HashSet<(string Method, string Path)> IgnoredEndpoints = new(new EndpointKeyComparer())
    {
        ("POST", "/api/file-upload-configs"),
        ("PUT", "/api/file-upload-configs"),
        ("DELETE", "/api/file-upload-configs/{}"),
        ("PUT", "/api/file-upload-configs/{}"),
        ("POST", "/api/file-upload-configs/scan-records/cleanup"),
        ("POST", "/api/file-upload-configs/scan-records/list"),
        ("GET", "/api/file-upload-configs/scan-records/statistics"),
        ("PUT", "/api/permissions/users/{}/email"),
    };

    private readonly TestWebApplicationFactory _factory;

    public ApiContractTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FrontendApiDefinitions_MatchBackendOpenApi()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"OpenAPI request failed ({(int)response.StatusCode} {response.StatusCode}). Response: {body}");
        }

        var openApiJson = await response.Content.ReadAsStringAsync();
        var openApiPaths = ParseOpenApiPaths(openApiJson);

        var repoRoot = ResolveRepoRoot();
        var apiRoot = Path.Combine(repoRoot, "app", "apps", "web", "src", "api");
        var frontendEndpoints = ExtractFrontendEndpoints(apiRoot, repoRoot);

        var missingPaths = new List<string>();
        var methodMismatches = new List<string>();
        foreach (var endpoint in frontendEndpoints.OrderBy(e => e.Path).ThenBy(e => e.Method))
        {
            if (IgnoredEndpoints.Contains((endpoint.Method, endpoint.Path)))
            {
                continue;
            }

            if (!openApiPaths.TryGetValue(endpoint.Path, out var methods))
            {
                missingPaths.Add($"{endpoint.Method} {endpoint.Path} (from {endpoint.Source})");
                continue;
            }

            if (!methods.Contains(endpoint.Method))
            {
                methodMismatches.Add($"{endpoint.Method} {endpoint.Path} (from {endpoint.Source})");
            }
        }

        if (methodMismatches.Count > 0)
        {
            Console.WriteLine("OpenAPI method mismatches (path exists, method differs):");
            Console.WriteLine(string.Join("\n", methodMismatches));
        }

        Assert.True(
            missingPaths.Count == 0,
            "OpenAPI is missing endpoints used by the frontend:\n" + string.Join("\n", missingPaths));
    }

    private static Dictionary<string, HashSet<string>> ParseOpenApiPaths(string openApiJson)
    {
        using var document = JsonDocument.Parse(openApiJson);
        var paths = document.RootElement.GetProperty("paths");
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths.EnumerateObject())
        {
            var normalizedPath = NormalizePathKey(path.Name);
            if (!result.TryGetValue(normalizedPath, out var methods))
            {
                methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                result[normalizedPath] = methods;
            }

            foreach (var method in path.Value.EnumerateObject())
            {
                if (IsHttpMethod(method.Name))
                {
                    methods.Add(method.Name.ToUpperInvariant());
                }
            }
        }

        return result;
    }

    private static bool IsHttpMethod(string name)
    {
        return name is "get" or "post" or "put" or "patch" or "delete" or "head" or "options";
    }

    private static IReadOnlyCollection<FrontendEndpoint> ExtractFrontendEndpoints(string apiRoot, string repoRoot)
    {
        var endpoints = new Dictionary<(string Method, string Path), FrontendEndpoint>(new EndpointKeyComparer());

        foreach (var file in Directory.EnumerateFiles(apiRoot, "*.ts", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            var source = Path.GetRelativePath(repoRoot, file);

            AddMatches(endpoints, content, source, "apiFetch", allowGeneric: true);
            AddMatches(endpoints, content, source, "fetch", allowGeneric: false);
        }

        return endpoints.Values.ToList();
    }

    private static void AddMatches(
        Dictionary<(string Method, string Path), FrontendEndpoint> endpoints,
        string content,
        string source,
        string functionName,
        bool allowGeneric)
    {
        foreach (var call in FindCalls(content, functionName, allowGeneric))
        {
            var rawPath = call.Path;
            var method = ResolveMethod(content, call.StartIndex);
            var normalizedPath = NormalizePath(rawPath);

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            normalizedPath = NormalizePathKey(normalizedPath);
            var key = (method, normalizedPath);
            if (!endpoints.ContainsKey(key))
            {
                endpoints[key] = new FrontendEndpoint(method, normalizedPath, source);
            }
        }
    }

    private static string ResolveMethod(string content, int startIndex)
    {
        var length = Math.Min(800, content.Length - startIndex);
        var window = content.Substring(startIndex, length);
        var match = MethodRegex.Match(window);
        if (match.Success)
        {
            return match.Groups["method"].Value.ToUpperInvariant();
        }

        return "GET";
    }

    private static string NormalizePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var path = ReplaceTemplateExpressions(rawPath.Trim(), expression =>
        {
            var trimmed = expression.Trim();

            if (trimmed.Equals("BASE_URL", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (trimmed.Contains("?") && trimmed.Contains(":"))
            {
                return string.Empty;
            }

            var parameter = ExtractParamName(trimmed);
            return string.IsNullOrWhiteSpace(parameter) ? string.Empty : $"{{{parameter}}}";
        });

        path = path.Replace("${BASE_URL}", string.Empty, StringComparison.Ordinal);
        path = path.Replace("BASE_URL", string.Empty, StringComparison.Ordinal);

        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            path = path.Substring(0, queryIndex);
        }

        path = path.Trim();
        if (path.Length == 0)
        {
            return string.Empty;
        }

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
        {
            path = path.TrimEnd('/');
        }

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            path = "/api" + path;
        }

        return path;
    }

    private static string NormalizePathKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = PathParameterRegex.Replace(path, "{}");
        normalized = Regex.Replace(normalized, @"(?<!/)\{\}", string.Empty);
        return normalized;
    }

    private static IEnumerable<CallMatch> FindCalls(string content, string functionName, bool allowGeneric)
    {
        var pattern = allowGeneric
            ? $@"\b{Regex.Escape(functionName)}\s*(?:<[^>]*>)?\s*\("
            : $@"\b{Regex.Escape(functionName)}\s*\(";

        foreach (Match match in Regex.Matches(content, pattern, RegexOptions.Compiled))
        {
            var index = match.Index + match.Length;
            while (index < content.Length && char.IsWhiteSpace(content[index]))
            {
                index++;
            }

            if (index >= content.Length)
            {
                continue;
            }

            var quote = content[index];
            if (quote != '\'' && quote != '"' && quote != '`')
            {
                continue;
            }

            if (!TryReadStringLiteral(content, index, out var literal, out _))
            {
                continue;
            }

            yield return new CallMatch(literal, match.Index);
        }
    }

    private static bool TryReadStringLiteral(string content, int startIndex, out string literal, out int endIndex)
    {
        literal = string.Empty;
        endIndex = startIndex;
        if (startIndex >= content.Length)
        {
            return false;
        }

        var quote = content[startIndex];
        if (quote != '\'' && quote != '"' && quote != '`')
        {
            return false;
        }

        if (quote == '`')
        {
            return TryReadTemplateLiteral(content, startIndex, out literal, out endIndex);
        }

        var builder = new System.Text.StringBuilder();
        var escaped = false;
        for (var index = startIndex + 1; index < content.Length; index++)
        {
            var current = content[index];
            if (escaped)
            {
                builder.Append(current);
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == quote)
            {
                literal = builder.ToString();
                endIndex = index + 1;
                return true;
            }

            builder.Append(current);
        }

        return false;
    }

    private static bool TryReadTemplateLiteral(string content, int startIndex, out string literal, out int endIndex)
    {
        literal = string.Empty;
        endIndex = startIndex;
        var builder = new System.Text.StringBuilder();
        var depth = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBacktick = false;
        var escaped = false;

        for (var index = startIndex + 1; index < content.Length; index++)
        {
            var current = content[index];
            if (escaped)
            {
                builder.Append(current);
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                builder.Append(current);
                continue;
            }

            if (depth == 0 && current == '`')
            {
                literal = builder.ToString();
                endIndex = index + 1;
                return true;
            }

            if (depth == 0 && current == '$' && index + 1 < content.Length && content[index + 1] == '{')
            {
                depth = 1;
                builder.Append("${");
                index++;
                continue;
            }

            if (depth > 0)
            {
                if (inSingleQuote)
                {
                    if (current == '\'')
                    {
                        inSingleQuote = false;
                    }
                    builder.Append(current);
                    continue;
                }

                if (inDoubleQuote)
                {
                    if (current == '"')
                    {
                        inDoubleQuote = false;
                    }
                    builder.Append(current);
                    continue;
                }

                if (inBacktick)
                {
                    if (current == '`')
                    {
                        inBacktick = false;
                    }
                    builder.Append(current);
                    continue;
                }

                if (current == '\'')
                {
                    inSingleQuote = true;
                    builder.Append(current);
                    continue;
                }

                if (current == '"')
                {
                    inDoubleQuote = true;
                    builder.Append(current);
                    continue;
                }

                if (current == '`')
                {
                    inBacktick = true;
                    builder.Append(current);
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                    builder.Append(current);
                    continue;
                }

                if (current == '}')
                {
                    depth--;
                    builder.Append(current);
                    continue;
                }
            }

            builder.Append(current);
        }

        return false;
    }

    private static string ReplaceTemplateExpressions(string template, Func<string, string> replaceExpression)
    {
        var result = new System.Text.StringBuilder();
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] == '$' && index + 1 < template.Length && template[index + 1] == '{')
            {
                index += 2;
                var depth = 1;
                var inSingleQuote = false;
                var inDoubleQuote = false;
                var inBacktick = false;
                var escaped = false;
                var expressionStart = index;

                for (; index < template.Length; index++)
                {
                    var current = template[index];
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (inSingleQuote)
                    {
                        if (current == '\'')
                        {
                            inSingleQuote = false;
                        }
                        continue;
                    }

                    if (inDoubleQuote)
                    {
                        if (current == '"')
                        {
                            inDoubleQuote = false;
                        }
                        continue;
                    }

                    if (inBacktick)
                    {
                        if (current == '`')
                        {
                            inBacktick = false;
                        }
                        continue;
                    }

                    if (current == '\'')
                    {
                        inSingleQuote = true;
                        continue;
                    }

                    if (current == '"')
                    {
                        inDoubleQuote = true;
                        continue;
                    }

                    if (current == '`')
                    {
                        inBacktick = true;
                        continue;
                    }

                    if (current == '{')
                    {
                        depth++;
                        continue;
                    }

                    if (current == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            break;
                        }
                    }
                }

                var expression = template.Substring(expressionStart, index - expressionStart);
                result.Append(replaceExpression(expression));
                continue;
            }

            result.Append(template[index]);
        }

        return result.ToString();
    }

    private static string ExtractParamName(string expression)
    {
        var matches = Regex.Matches(expression, @"[A-Za-z_][A-Za-z0-9_]*");
        if (matches.Count == 0)
        {
            return "param";
        }

        return matches[matches.Count - 1].Value;
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "app", "apps", "web", "src", "api");
            var apiRootExists = Directory.Exists(candidate);
            var solutionExists = File.Exists(Path.Combine(directory.FullName, "SupplierSystem", "SupplierSystem.sln"));

            if (apiRootExists && solutionExists)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from test base directory.");
    }

    private sealed class EndpointKeyComparer : IEqualityComparer<(string Method, string Path)>
    {
        public bool Equals((string Method, string Path) x, (string Method, string Path) y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Method, y.Method)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Path, y.Path);
        }

        public int GetHashCode((string Method, string Path) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Method),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path));
        }
    }
    private sealed record FrontendEndpoint(string Method, string Path, string Source);

    private readonly record struct CallMatch(string Path, int StartIndex);
}


