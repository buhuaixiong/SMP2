using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Repositories;

public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(SupplierSystemDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalized = id.Trim().ToLower();
        var user = await DbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id.ToLower() == normalized, cancellationToken);

        return Sanitize(user);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLower();
        var user = await DbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalized, cancellationToken);

        return Sanitize(user);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalized = username.Trim().ToLower();
        var user = await DbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, cancellationToken);

        return Sanitize(user);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = await DbContext.Users.AsNoTracking()
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.Password = string.Empty;
        }

        return users;
    }

    public override async Task<User> CreateAsync(User user, CancellationToken cancellationToken)
    {
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync(cancellationToken);
        return SanitizeCopy(user);
    }

    public override async Task<User> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        DbContext.Users.Update(user);
        await DbContext.SaveChangesAsync(cancellationToken);
        return SanitizeCopy(user);
    }

    public override async Task<bool> DeleteAsync(object id, CancellationToken cancellationToken)
    {
        if (id is not string userId || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        DbContext.Users.Remove(user);
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var user = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.Status = status?.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetMustChangePasswordAsync(string id, bool mustChangePassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var user = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.MustChangePassword = mustChangePassword;
        user.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static User? Sanitize(User? user)
    {
        if (user == null)
        {
            return null;
        }

        user.Password = string.Empty;
        return user;
    }

    private static User SanitizeCopy(User user)
    {
        return new User
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Role = user.Role,
            Password = string.Empty,
            AuthVersion = user.AuthVersion,
            SupplierId = user.SupplierId,
            TenantId = user.TenantId,
            Email = user.Email,
            MustChangePassword = user.MustChangePassword,
            ForcePasswordReset = user.ForcePasswordReset,
            InitialPasswordIssuedAt = user.InitialPasswordIssuedAt,
            TempAccountId = user.TempAccountId,
            AccountType = user.AccountType,
            RelatedApplicationId = user.RelatedApplicationId,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Department = user.Department,
            Status = user.Status,
            LastLoginAt = user.LastLoginAt
        };
    }
}
