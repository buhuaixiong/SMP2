using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Models.Security;

public sealed record RequestSecurityContext(string? ClientIp, AuthUser? User);
