using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class ContractsController
{
    [HttpGet("{id:int}/versions")]
    public async Task<IActionResult> ListVersions(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var contract = await _dbContext.Contracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract == null)
        {
            return NotFound(new { message = "Contract not found." });
        }

        if (user?.SupplierId.HasValue == true &&
            !HasAnyPermission(user, StaffContractPermissions) &&
            contract.SupplierId != user.SupplierId)
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        var versions = await _dbContext.ContractVersions.AsNoTracking()
            .Where(v => v.ContractId == id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        return Ok(versions);
    }

    [HttpPost("{id:int}/versions")]
    public async Task<IActionResult> UploadVersion(
        int id,
        IFormFile file,
        [FromForm] string? changeLog,
        [FromForm] string? createdBy,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Contract document is required." });
        }

        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var contract = await _dbContext.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract == null)
        {
            return NotFound(new { message = "Contract not found." });
        }

        if (!HasAnyPermission(user, StaffContractPermissions) &&
            !(user?.SupplierId.HasValue == true && user.SupplierId == contract.SupplierId &&
              HasAnyPermission(user, Permissions.SupplierContractUpload)))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        var contractsRoot = UploadPathHelper.GetContractsRoot(_environment);
        var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(contractsRoot, storedName);

        try
        {
            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var maxVersion = await _dbContext.ContractVersions
                .Where(v => v.ContractId == id)
                .MaxAsync(v => (int?)v.VersionNumber, cancellationToken) ?? 0;
            var nextVersion = maxVersion + 1;

            var now = DateTime.UtcNow.ToString("o");
            var actor = user?.Name ?? createdBy ?? "system";

            var version = new ContractVersion
            {
                ContractId = id,
                VersionNumber = nextVersion,
                StoredName = storedName,
                OriginalName = file.FileName,
                ChangeLog = changeLog,
                CreatedAt = now,
                CreatedBy = actor,
                FileSize = file.Length
            };

            _dbContext.ContractVersions.Add(version);
            contract.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var versions = await _dbContext.ContractVersions.AsNoTracking()
                .Where(v => v.ContractId == id)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, versions);
        }
        catch (Exception)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return StatusCode(500, new { message = "Failed to upload contract version." });
        }
    }

    [HttpDelete("{id:int}/versions/{versionId:int}")]
    public async Task<IActionResult> DeleteVersion(int id, int versionId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var contract = await _dbContext.Contracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract == null)
        {
            return NotFound(new { message = "Contract not found." });
        }

        if (!HasAnyPermission(user, StaffContractPermissions) &&
            !(user?.SupplierId.HasValue == true && user.SupplierId == contract.SupplierId &&
              HasAnyPermission(user, Permissions.SupplierContractUpload)))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        var version = await _dbContext.ContractVersions.FirstOrDefaultAsync(
            v => v.Id == versionId && v.ContractId == id,
            cancellationToken);
        if (version == null)
        {
            return NotFound(new { message = "Contract version not found." });
        }

        _dbContext.ContractVersions.Remove(version);
        await _dbContext.SaveChangesAsync(cancellationToken);

        DeleteContractFiles([version]);
        return NoContent();
    }
}
