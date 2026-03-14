using SupplierSystem.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Core registrations live in AddSupplierSystemApi():
// AddSingleton<ITokenBlacklistService, TokenBlacklistService>()
// AddSingleton<IRateLimitService, RateLimitService>()
// AddScoped<AdminBootstrapStartupGuard>()
builder.ConfigureApiHost();
builder.AddSupplierSystemApi();

// Keep UploadPathHelper consistent with configuration/env vars.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UPLOADS_PATH")))
{
    var uploadsPath = builder.Configuration["Uploads:Path"];
    if (string.IsNullOrWhiteSpace(uploadsPath))
    {
        uploadsPath = Environment.GetEnvironmentVariable("UPLOAD_DIR");
    }
    if (string.IsNullOrWhiteSpace(uploadsPath))
    {
        uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
    }

    Environment.SetEnvironmentVariable("UPLOADS_PATH", uploadsPath);
}

var app = builder.Build();

// Legacy startup flow now delegates through InitializeSupplierSystemAsync():
// await EnsureAdminBootstrapInitializedAsync(app);
await app.InitializeSupplierSystemAsync();
app.UseSupplierSystemApi();

app.Run();
