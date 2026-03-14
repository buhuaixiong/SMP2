using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class OrganizationalUnitDataService
{
    private readonly SupplierSystemDbContext _dbContext;

    public OrganizationalUnitDataService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<OrganizationalUnit> QueryUnits(bool asNoTracking = true)
    {
        var query = _dbContext.OrganizationalUnits.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<OrganizationalUnitMember> QueryMembers(bool asNoTracking = true)
    {
        var query = _dbContext.OrganizationalUnitMembers.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<User> QueryUsers(bool asNoTracking = true)
    {
        var query = _dbContext.Users.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public void AddUnit(OrganizationalUnit unit) => _dbContext.OrganizationalUnits.Add(unit);
    public void AddMember(OrganizationalUnitMember member) => _dbContext.OrganizationalUnitMembers.Add(member);
    public void RemoveMember(OrganizationalUnitMember member) => _dbContext.OrganizationalUnitMembers.Remove(member);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
