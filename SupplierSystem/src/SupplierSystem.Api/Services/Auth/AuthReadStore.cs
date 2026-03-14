using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Auth;

public sealed class AuthReadStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public AuthReadStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<TokenBlacklistEntry> QueryTokenBlacklist(bool asNoTracking = true)
    {
        var query = _dbContext.TokenBlacklist.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<ExternalInvitationDetails?> GetInvitationDetailsAsync(string token, CancellationToken cancellationToken)
    {
        return (from entry in _dbContext.ExternalSupplierInvitations.AsNoTracking()
                join rfq in _dbContext.Rfqs.AsNoTracking() on (long)entry.RfqId equals rfq.Id
                where entry.InvitationToken == token
                select new ExternalInvitationDetails
                {
                    Email = entry.Email,
                    CompanyName = entry.CompanyName,
                    ContactPerson = entry.ContactPerson,
                    RfqId = entry.RfqId,
                    ExpiresAt = entry.ExpiresAt,
                    RegistrationCompleted = entry.RegistrationCompleted,
                    RfqTitle = rfq.Title,
                    RfqValidUntil = rfq.ValidUntil,
                }).FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed class ExternalInvitationDetails
{
    public string? Email { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public int RfqId { get; set; }
    public string? ExpiresAt { get; set; }
    public bool? RegistrationCompleted { get; set; }
    public string? RfqTitle { get; set; }
    public string? RfqValidUntil { get; set; }
}
