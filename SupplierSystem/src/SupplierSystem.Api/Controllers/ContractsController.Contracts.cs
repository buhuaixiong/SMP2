using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class ContractsController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ContractResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListContracts(
        [FromQuery] int? supplierId,
        [FromQuery] string? status,
        [FromQuery] string? expiresBefore,
        [FromQuery] string? q,
        [FromQuery] string? isMandatory,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (user?.SupplierId.HasValue == true && !HasAnyPermission(user, StaffContractPermissions))
        {
            supplierId = user.SupplierId;
        }

        var query = _dbContext.Contracts.AsNoTracking();

        if (supplierId.HasValue)
        {
            query = query.Where(c => c.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.Title ?? string.Empty, $"%{keyword}%") ||
                EF.Functions.Like(c.AgreementNumber ?? string.Empty, $"%{keyword}%") ||
                EF.Functions.Like(c.Notes ?? string.Empty, $"%{keyword}%"));
        }

        var contracts = await query.ToListAsync(cancellationToken);

        if (TryParseBoolean(isMandatory, out var isMandatoryValue))
        {
            contracts = contracts.Where(c => c.IsMandatory == isMandatoryValue).ToList();
        }

        if (TryParseDate(expiresBefore, out var expiresBeforeDate))
        {
            contracts = contracts
                .Where(c => TryParseDate(c.EffectiveTo, out var effectiveTo) && effectiveTo <= expiresBeforeDate)
                .ToList();
        }

        contracts = contracts
            .OrderByDescending(c => ResolveSortDate(c.UpdatedAt) ?? ResolveSortDate(c.CreatedAt) ?? DateTime.MinValue)
            .ToList();

        var contractIds = contracts.Select(c => c.Id).ToList();
        var versions = await _dbContext.ContractVersions.AsNoTracking()
            .Where(v => contractIds.Contains(v.ContractId))
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        var versionMap = versions
            .GroupBy(v => v.ContractId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var response = contracts
            .Select(contract => MapContract(contract, versionMap))
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContract(int id, CancellationToken cancellationToken)
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

        return Ok(MapContract(contract, versions));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateContract(
        [FromBody] ContractCreateRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: false);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (request.SupplierId <= 0 || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.AgreementNumber))
        {
            return BadRequest(new { message = "supplierId, title and agreementNumber are required." });
        }

        var supplierExists = await _dbContext.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!supplierExists)
        {
            return NotFound(new { message = "Supplier not found." });
        }

        var agreementExists = await _dbContext.Contracts.AnyAsync(
            c => c.AgreementNumber == request.AgreementNumber,
            cancellationToken);
        if (agreementExists)
        {
            return Conflict(new { message = "Contract with the same agreement number already exists." });
        }

        var now = DateTime.UtcNow.ToString("o");
        var actorName = user?.Name ?? request.CreatedBy ?? "system";

        var contract = new Contract
        {
            SupplierId = request.SupplierId,
            Title = request.Title?.Trim(),
            AgreementNumber = request.AgreementNumber?.Trim(),
            Amount = request.Amount,
            Currency = request.Currency,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "draft" : request.Status,
            PaymentCycle = request.PaymentCycle,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            AutoRenew = request.AutoRenew ?? false,
            IsMandatory = request.IsMandatory ?? false,
            Notes = request.Notes,
            CreatedBy = actorName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Contracts.Add(contract);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, MapContract(contract, Array.Empty<ContractVersion>()));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContract(
        int id,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: false);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var contract = await _dbContext.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract == null)
        {
            return NotFound(new { message = "Contract not found." });
        }

        var updated = false;
        updated |= TryAssignString(payload, "title", value => contract.Title = value?.Trim());
        updated |= TryAssignDecimal(payload, "amount", value => contract.Amount = value);
        updated |= TryAssignString(payload, "currency", value => contract.Currency = value);
        updated |= TryAssignString(payload, "status", value => contract.Status = value);
        updated |= TryAssignString(payload, "paymentCycle", value => contract.PaymentCycle = value);
        updated |= TryAssignString(payload, "effectiveFrom", value => contract.EffectiveFrom = value);
        updated |= TryAssignString(payload, "effectiveTo", value => contract.EffectiveTo = value);
        updated |= TryAssignString(payload, "notes", value => contract.Notes = value);
        updated |= TryAssignBool(payload, "autoRenew", value => contract.AutoRenew = value);
        updated |= TryAssignBool(payload, "isMandatory", value => contract.IsMandatory = value);

        if (!updated)
        {
            return BadRequest(new { message = "No changes provided." });
        }

        contract.UpdatedAt = DateTime.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        var versions = await _dbContext.ContractVersions.AsNoTracking()
            .Where(v => v.ContractId == id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        return Ok(MapContract(contract, versions));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteContract(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireContractAccess(user, allowSupplier: false);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var contract = await _dbContext.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract == null)
        {
            return NotFound(new { message = "Contract not found." });
        }

        var versions = await _dbContext.ContractVersions.AsNoTracking()
            .Where(v => v.ContractId == id)
            .ToListAsync(cancellationToken);

        _dbContext.Contracts.Remove(contract);
        await _dbContext.SaveChangesAsync(cancellationToken);

        DeleteContractFiles(versions);
        return NoContent();
    }

    private static ContractResponse MapContract(Contract contract, IReadOnlyDictionary<int, List<ContractVersion>> versionMap)
    {
        versionMap.TryGetValue(contract.Id, out var versions);
        return MapContract(contract, versions ?? new List<ContractVersion>());
    }

    private static ContractResponse MapContract(Contract contract, IReadOnlyList<ContractVersion> versions)
    {
        return new ContractResponse
        {
            Id = contract.Id,
            SupplierId = contract.SupplierId,
            Title = contract.Title,
            AgreementNumber = contract.AgreementNumber,
            Amount = contract.Amount,
            Currency = contract.Currency,
            Status = contract.Status,
            PaymentCycle = contract.PaymentCycle,
            EffectiveFrom = contract.EffectiveFrom,
            EffectiveTo = contract.EffectiveTo,
            AutoRenew = contract.AutoRenew,
            IsMandatory = contract.IsMandatory,
            CreatedBy = contract.CreatedBy,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt,
            Notes = contract.Notes,
            Versions = versions.ToList()
        };
    }

    private sealed class ContractResponse
    {
        public int Id { get; set; }
        public int? SupplierId { get; set; }
        public string? Title { get; set; }
        public string? AgreementNumber { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string? Status { get; set; }
        public string? PaymentCycle { get; set; }
        public string? EffectiveFrom { get; set; }
        public string? EffectiveTo { get; set; }
        public bool AutoRenew { get; set; }
        public bool IsMandatory { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedAt { get; set; }
        public string? UpdatedAt { get; set; }
        public string? Notes { get; set; }
        public List<ContractVersion> Versions { get; set; } = new();
    }

    public sealed class ContractCreateRequest
    {
        public int SupplierId { get; set; }
        public string? Title { get; set; }
        public string? AgreementNumber { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string? Status { get; set; }
        public string? PaymentCycle { get; set; }
        public string? EffectiveFrom { get; set; }
        public string? EffectiveTo { get; set; }
        public bool? AutoRenew { get; set; }
        public bool? IsMandatory { get; set; }
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
    }
}
