using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Authorization;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Middleware;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.ChangeRequests;
using SupplierSystem.Api.Services.FileUploads;
using SupplierSystem.Api.Services.Reminders;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Api.Services.Scheduling;
using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;
using SupplierSystem.Infrastructure.Repositories;
using SupplierSystem.Infrastructure.Services;
using Microsoft.Extensions.FileProviders;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// 配置 Kestrel 请求体大小限�?(10MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

// 配置 JSON 序列化器选项
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<GlobalExceptionFilter>();
        options.Filters.Add<ApiResponseFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 64;
    });
builder.Services.AddOpenApi();
builder.Services.AddAuthorization(options =>
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
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, BuyerAssignmentsReadHandler>();

var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]
    ?? Environment.GetEnvironmentVariable("CORS_ORIGINS")
    ?? Environment.GetEnvironmentVariable("CLIENT_ORIGIN");
var allowedOrigins = string.IsNullOrWhiteSpace(corsOrigins)
    ? new[] { "http://localhost", "http://localhost:5173", "http://127.0.0.1:5173" }
    : corsOrigins
        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(origin => origin.Trim())
        .Where(origin => origin.Length > 0)
        .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration.GetConnectionString("SupplierSystem");
builder.Services.AddDbContext<SupplierSystemDbContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<RfqStateMachine>();
builder.Services.AddScoped<QuoteStateMachine>();
builder.Services.AddScoped<ReconciliationStateMachine>();
builder.Services.AddScoped<RfqService>();
builder.Services.AddScoped<RfqQuoteService>();
builder.Services.AddScoped<RfqPriceAuditService>();
builder.Services.AddScoped<LineItemWorkflowService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<PublicRfqService>();
builder.Services.AddScoped<SupplierSystem.Api.Services.Registrations.SupplierRegistrationService>();

builder.Services.AddScoped<SystemLockdownService>();

builder.Services.AddScoped<NotificationService>();

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

var app = builder.Build();
// Support both root deployment and reverse-proxy subpath deployment.
app.UsePathBase("/smp");
app.UsePathBase("/smpBackend");

LogConnectionString(app, connectionString);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}
app.UseCors("default");
var webRootCandidates = new[]
{
    Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
    Path.Combine(AppContext.BaseDirectory, "wwwroot"),
};
var webRootPath = webRootCandidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "index.html")))
    ?? webRootCandidates[0];
var spaIndexPath = Path.Combine(webRootPath, "index.html");
var serveSpa = File.Exists(spaIndexPath);
PhysicalFileProvider? spaFileProvider = null;
if (serveSpa)
{
    spaFileProvider = new PhysicalFileProvider(webRootPath);
    app.Environment.WebRootPath = webRootPath;
    app.Environment.WebRootFileProvider = spaFileProvider;

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = spaFileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = spaFileProvider
    });
}
app.Logger.LogInformation(
    "SPA static root: {WebRootPath}. Index: {IndexPath}. Exists: {Exists}.",
    webRootPath,
    spaIndexPath,
    serveSpa);
app.UseMiddleware<AccountLockoutMiddleware>();
app.UseAuthentication();
app.UseMiddleware<SecurityContextMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<LockdownGuardMiddleware>();
app.UseAuthorization();

var uploadsPath = builder.Configuration["Uploads:Path"]
    ?? Environment.GetEnvironmentVariable("UPLOADS_PATH")
    ?? Path.Combine(app.Environment.ContentRootPath, "uploads");
try
{
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });
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
        serveSpa
    }));
}

if (serveSpa)
{
    app.MapFallback((HttpContext context) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        return Results.File(spaIndexPath, "text/html; charset=utf-8");
    });
}

app.Run();

static void LogConnectionString(WebApplication app, string? connectionString)
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

static string MaskConnectionString(string connectionString)
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




