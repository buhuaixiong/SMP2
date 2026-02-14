using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class OrganizationalUnitsController
{
    private static OrgUnitResponse ToResponse(OrganizationalUnit unit, int memberCount, int childCount)
    {
        return new OrgUnitResponse
        {
            Id = unit.Id,
            Code = unit.Code,
            Name = unit.Name,
            Type = unit.Type,
            ParentId = unit.ParentId,
            Level = unit.Level,
            Path = unit.Path,
            Description = unit.Description,
            AdminIds = ParseAdminIds(unit.AdminIds),
            Function = unit.Function,
            Category = unit.Category,
            Region = unit.Region,
            IsActive = unit.IsActive ? 1 : 0,
            CreatedAt = unit.CreatedAt,
            CreatedBy = unit.CreatedBy,
            UpdatedAt = unit.UpdatedAt,
            UpdatedBy = unit.UpdatedBy,
            DeletedAt = unit.DeletedAt,
            DeletedBy = unit.DeletedBy,
            MemberCount = memberCount,
            SupplierCount = 0,
            ContractCount = 0,
            ChildCount = childCount
        };
    }

    private static string? NormalizeRequiredString(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            return null;
        }

        return trimmed;
    }

    private static string? NormalizeOptionalString(string? value, int maxLength)
    {
        if (value == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            return string.Empty;
        }

        return trimmed;
    }

    private static List<string> ParseAdminIds(string? adminIds)
    {
        if (string.IsNullOrWhiteSpace(adminIds))
        {
            return new List<string>();
        }

        return adminIds
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeAdminIds(IEnumerable<string>? adminIds)
    {
        if (adminIds == null)
        {
            return null;
        }

        var normalized = adminIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return null;
        }

        return string.Join(",", normalized);
    }

    private static List<string> ResolveAdminIds(JsonElement body, out bool adminIdsSpecified, out bool adminIdSpecified)
    {
        adminIdsSpecified = TryReadStringList(body, out var adminIds, "adminIds", "admin_ids");
        if (adminIdsSpecified)
        {
            adminIdSpecified = false;
            return adminIds;
        }

        adminIdSpecified = TryReadString(body, out var adminId, "adminId", "admin_id");
        if (!adminIdSpecified)
        {
            return new List<string>();
        }

        return string.IsNullOrWhiteSpace(adminId)
            ? new List<string>()
            : new List<string> { adminId.Trim() };
    }

    private static string BuildPath(OrganizationalUnit unit, Dictionary<int, OrganizationalUnit>? unitMap)
    {
        if (!string.IsNullOrWhiteSpace(unit.Path))
        {
            return unit.Path!;
        }

        var ids = new List<int> { unit.Id };
        var current = unit;
        var guard = 0;

        while (current.ParentId.HasValue && guard < 50)
        {
            guard++;
            if (unitMap != null && unitMap.TryGetValue(current.ParentId.Value, out var parent))
            {
                ids.Add(parent.Id);
                current = parent;
                continue;
            }

            break;
        }

        ids.Reverse();
        return "/" + string.Join("/", ids) + "/";
    }

    private static Dictionary<int, List<OrganizationalUnit>> BuildChildrenMap(List<OrganizationalUnit> units)
    {
        var map = new Dictionary<int, List<OrganizationalUnit>>();
        foreach (var unit in units)
        {
            if (!unit.ParentId.HasValue)
            {
                continue;
            }

            if (!map.TryGetValue(unit.ParentId.Value, out var children))
            {
                children = new List<OrganizationalUnit>();
                map[unit.ParentId.Value] = children;
            }

            children.Add(unit);
        }

        return map;
    }

    private static void CollectSubtreeIds(int rootId, Dictionary<int, List<OrganizationalUnit>> childrenMap, HashSet<int> results)
    {
        if (!results.Add(rootId))
        {
            return;
        }

        if (!childrenMap.TryGetValue(rootId, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            CollectSubtreeIds(child.Id, childrenMap, results);
        }
    }

    private static int GetMaxDepth(int rootId, Dictionary<int, List<OrganizationalUnit>> childrenMap)
    {
        if (!childrenMap.TryGetValue(rootId, out var children) || children.Count == 0)
        {
            return 1;
        }

        var max = 1;
        foreach (var child in children)
        {
            max = Math.Max(max, 1 + GetMaxDepth(child.Id, childrenMap));
        }

        return max;
    }

    private static void UpdateSubtree(
        OrganizationalUnit unit,
        Dictionary<int, List<OrganizationalUnit>> childrenMap,
        string parentPath,
        int parentLevel,
        string updatedAt,
        string updatedBy)
    {
        unit.Level = parentLevel + 1;
        unit.Path = $"{parentPath}{unit.Id}/";
        unit.UpdatedAt = updatedAt;
        unit.UpdatedBy = updatedBy;

        if (!childrenMap.TryGetValue(unit.Id, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            UpdateSubtree(child, childrenMap, unit.Path, unit.Level, updatedAt, updatedBy);
        }
    }

    private async Task<List<OrgUnitResponse>> LoadAncestorsAsync(
        OrganizationalUnit unit,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var ids = ParsePathIds(unit.Path);
        ids.Remove(unit.Id);
        if (ids.Count == 0)
        {
            return new List<OrgUnitResponse>();
        }

        var ancestors = await _dbContext.OrganizationalUnits.AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.TenantId == tenantId && item.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var map = ancestors.ToDictionary(item => item.Id);
        var ordered = new List<OrgUnitResponse>();
        foreach (var ancestorId in ids)
        {
            if (map.TryGetValue(ancestorId, out var ancestor))
            {
                ordered.Add(ToResponse(ancestor, 0, 0));
            }
        }

        return ordered;
    }

    private static List<int> ParsePathIds(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new List<int>();
        }

        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => int.TryParse(segment, out var value) ? value : 0)
            .Where(value => value > 0)
            .ToList();
    }

    private static bool TryReadString(JsonElement body, out string? value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                value = null;
                return true;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString();
                return true;
            }

            value = element.ToString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadBool(JsonElement body, out bool? value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                value = null;
                return true;
            }

            if (element.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (element.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
            {
                value = numeric != 0;
                return true;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var raw = element.GetString();
                if (bool.TryParse(raw, out var parsed))
                {
                    value = parsed;
                    return true;
                }

                if (raw == "1")
                {
                    value = true;
                    return true;
                }

                if (raw == "0")
                {
                    value = false;
                    return true;
                }
            }

            value = null;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadInt(JsonElement body, out int? value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                value = null;
                return true;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
            {
                value = numeric;
                return true;
            }

            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out numeric))
            {
                value = numeric;
                return true;
            }

            value = null;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadStringList(JsonElement body, out List<string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var element))
            {
                continue;
            }

            values = new List<string>();
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var entry = item.GetString();
                        if (!string.IsNullOrWhiteSpace(entry))
                        {
                            values.Add(entry.Trim());
                        }
                    }
                    else if (item.ValueKind != JsonValueKind.Null && item.ValueKind != JsonValueKind.Undefined)
                    {
                        var entry = item.ToString();
                        if (!string.IsNullOrWhiteSpace(entry))
                        {
                            values.Add(entry.Trim());
                        }
                    }
                }

                return true;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var single = element.GetString();
                if (!string.IsNullOrWhiteSpace(single))
                {
                    values.Add(single.Trim());
                }
                return true;
            }

            values = new List<string>();
            return true;
        }

        values = new List<string>();
        return false;
    }

    private static bool TryReadIntList(JsonElement body, out List<int> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var element))
            {
                continue;
            }

            values = new List<int>();
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var numeric))
                    {
                        values.Add(numeric);
                        continue;
                    }

                    if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out numeric))
                    {
                        values.Add(numeric);
                    }
                }

                return true;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var singleNumeric))
            {
                values.Add(singleNumeric);
                return true;
            }

            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out singleNumeric))
            {
                values.Add(singleNumeric);
                return true;
            }

            values = new List<int>();
            return true;
        }

        values = new List<int>();
        return false;
    }

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (value == "1")
        {
            result = true;
            return true;
        }

        if (value == "0")
        {
            result = false;
            return true;
        }

        return false;
    }

    private static bool TryParseInt(string? value, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return int.TryParse(value, out result);
    }
}
