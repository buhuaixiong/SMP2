namespace SupplierSystem.Application.Interfaces;

public interface IRateLimitService
{
    /// <summary>
    /// 检查是否应该阻止请求
    /// </summary>
    /// <param name="identifier">客户端标识符（IP或用户名）</param>
    /// <param name="endpoint">端点名称</param>
    /// <returns>如果应该阻止返回 true</returns>
    bool ShouldBlock(string identifier, string endpoint);

    /// <summary>
    /// 记录一次请求
    /// </summary>
    void RecordRequest(string identifier, string endpoint);

    /// <summary>
    /// 重置指定标识符的速率限制计数器
    /// </summary>
    void Reset(string identifier, string endpoint);
}

public sealed class RateLimitResult
{
    public bool IsLimited { get; init; }
    public int RetryAfterSeconds { get; init; }
    public int RequestCount { get; init; }
    public int Limit { get; init; }
}
