using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Application.Interfaces;

/// <summary>
/// 供应商服务接口 - 对齐Node.js API完整功能
/// </summary>
public interface ISupplierService
{
    // === 基础CRUD ===

    /// <summary>
    /// 根据ID获取供应商详情
    /// </summary>
    Task<SupplierResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// 获取供应商列表（支持过滤和分页）
    /// </summary>
    Task<SupplierListResponse> ListSuppliersAsync(
        SupplierListQuery query,
        AuthUser? user,
        CancellationToken cancellationToken);

    /// <summary>
    /// 创建供应商
    /// </summary>
    Task<SupplierResponse> CreateSupplierAsync(
        CreateSupplierRequest request,
        string createdBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// 更新供应商
    /// </summary>
    Task<SupplierResponse?> UpdateSupplierAsync(
        int id,
        CreateSupplierRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 更新供应商状态
    /// </summary>
    Task<SupplierResponse?> UpdateSupplierStatusAsync(
        int id,
        UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 审批供应商
    /// </summary>
    Task<SupplierResponse?> ApproveSupplierAsync(
        int id,
        ApproveSupplierRequest request,
        string approver,
        CancellationToken cancellationToken);

    // === 标签管理 ===

    /// <summary>
    /// 获取所有标签
    /// </summary>
    Task<List<TagResponse>> GetTagsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 创建标签
    /// </summary>
    Task<TagResponse> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 更新标签
    /// </summary>
    Task<TagResponse?> UpdateTagAsync(int id, UpdateTagRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 删除标签
    /// </summary>
    Task<bool> DeleteTagAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// 批量分配标签到供应商
    /// </summary>
    Task<BatchTagResult> BatchAssignTagAsync(int tagId, List<int> supplierIds, CancellationToken cancellationToken);

    /// <summary>
    /// 批量移除供应商标签
    /// </summary>
    Task<BatchTagResult> BatchRemoveTagAsync(int tagId, List<int> supplierIds, CancellationToken cancellationToken);

    /// <summary>
    /// 获取指定标签下的所有供应商
    /// </summary>
    Task<List<SupplierResponse>> GetSuppliersByTagAsync(int tagId, CancellationToken cancellationToken);

    /// <summary>
    /// 更新供应商标签
    /// </summary>
    Task<List<TagResponse>> UpdateSupplierTagsAsync(int id, List<string> tags, CancellationToken cancellationToken);

    // === 文档管理 ===

    /// <summary>
    /// 获取供应商文档列表
    /// </summary>
    Task<List<SupplierDocumentResponse>> GetSupplierDocumentsAsync(int supplierId, CancellationToken cancellationToken);

    /// <summary>
    /// 上传供应商文档
    /// </summary>
    Task<SupplierDocumentResponse> UploadDocumentAsync(
        int supplierId,
        string fileName,
        string storedName,
        long fileSize,
        string uploadedBy,
        UploadDocumentRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 续期供应商文档
    /// </summary>
    Task<SupplierDocumentResponse?> RenewDocumentAsync(
        int supplierId,
        int documentId,
        string storedName,
        string originalName,
        long fileSize,
        string uploadedBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// 删除供应商文档
    /// </summary>
    /// <summary>
    /// Update supplier document
    /// </summary>
    Task<SupplierDocumentResponse?> UpdateDocumentAsync(
        int supplierId,
        int documentId,
        UpdateDocumentRequest request,
        string? storedName,
        string? originalName,
        long? fileSize,
        string updatedBy,
        CancellationToken cancellationToken);

    Task<bool> DeleteDocumentAsync(int supplierId, int documentId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取文档下载信息
    /// </summary>
    Task<SupplierDocumentResponse?> GetDocumentDownloadInfoAsync(int supplierId, int documentId, CancellationToken cancellationToken);

    // === 草稿管理 ===

    /// <summary>
    /// 保存供应商草稿
    /// </summary>
    Task<bool> SaveDraftAsync(int supplierId, object draftData, string updatedBy, CancellationToken cancellationToken);

    /// <summary>
    /// 获取供应商草稿
    /// </summary>
    Task<object?> GetDraftAsync(int supplierId, CancellationToken cancellationToken);

    /// <summary>
    /// 删除供应商草稿
    /// </summary>
    Task<bool> DeleteDraftAsync(int supplierId, CancellationToken cancellationToken);

    // === 历史记录 ===

    /// <summary>
    /// 获取供应商变更历史
    /// </summary>
    Task<SupplierHistoryResponse> GetHistoryAsync(int supplierId, int limit, int offset, CancellationToken cancellationToken);

    // === 统计信息 ===

    /// <summary>
    /// 获取供应商统计概览
    /// </summary>
    Task<SupplierStatsOverviewResponse> GetStatsOverviewAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 获取供应商评分统计
    /// </summary>
    Task<SupplierStatsResponse?> GetSupplierStatsAsync(int supplierId, CancellationToken cancellationToken);

    /// <summary>
    /// Batch update supplier completeness scores
    /// </summary>
    Task<SupplierCompletenessUpdateResult> UpdateAllSuppliersCompletenessAsync(
        string triggeredBy,
        string triggerReason,
        CancellationToken cancellationToken);

    // === 审批预览 ===

    /// <summary>
    /// 预览审批流程
    /// </summary>
    Task<object> PreviewApprovalAsync(object request, CancellationToken cancellationToken);

    // === 临时账户 ===

    /// <summary>
    /// 发行临时账户
    /// </summary>
    Task<object> IssueTempAccountAsync(int supplierId, object request, CancellationToken cancellationToken);

    /// <summary>
    /// 确定供应商代码
    /// </summary>
    Task<object> FinalizeSupplierCodeAsync(int supplierId, object request, CancellationToken cancellationToken);

    // === 基准数据 ===

    /// <summary>
    /// 获取供应商基准数据
    /// </summary>
    Task<object> GetBenchmarksAsync(CancellationToken cancellationToken);

    // === 批量导入 ===

    /// <summary>
    /// 从Excel文件导入供应商
    /// </summary>
    Task<SupplierImportResponse> ImportSuppliersFromExcelAsync(
        byte[] fileContent,
        string fileName,
        string importedBy,
        CancellationToken cancellationToken);
}

/// <summary>
/// 批量标签操作结果
/// </summary>
public class BatchTagResult
{
    public int Added { get; set; }
    public int Removed { get; set; }
    public int Skipped { get; set; }
}

/// <summary>
/// 供应商历史记录响应
/// </summary>
public class SupplierHistoryResponse
{
    public List<SupplierHistoryEntry> History { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>
/// 供应商历史条目
/// </summary>
public class SupplierHistoryEntry
{
    public int Id { get; set; }
    public string? Timestamp { get; set; }
    public string? Action { get; set; }
    public string? Actor { get; set; }
    public string? ActorName { get; set; }
    public string? Changes { get; set; }
}
