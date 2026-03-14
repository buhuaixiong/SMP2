using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Templates;

public sealed class TemplateDocumentStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public TemplateDocumentStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<TemplateDocument>> ListActiveAsync(CancellationToken cancellationToken)
    {
        return _dbContext.TemplateDocuments
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);
    }

    public Task<List<TemplateDocument>> ListHistoryAsync(string templateCode, CancellationToken cancellationToken)
    {
        return _dbContext.TemplateDocuments
            .AsNoTracking()
            .Where(t => t.TemplateCode == templateCode)
            .OrderByDescending(t => t.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<TemplateDocument>> ListActiveByCodeForUpdateAsync(string templateCode, CancellationToken cancellationToken)
    {
        return _dbContext.TemplateDocuments
            .Where(t => t.TemplateCode == templateCode && t.IsActive)
            .ToListAsync(cancellationToken);
    }

    public void Add(TemplateDocument document)
    {
        _dbContext.TemplateDocuments.Add(document);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}
