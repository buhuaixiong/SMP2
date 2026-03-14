namespace SupplierSystem.Application.Models.Auth;

public sealed class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "supplier-system";
    public string Audience { get; set; } = "supplier-system";
    public string ExpiresIn { get; set; } = "8h";
}
