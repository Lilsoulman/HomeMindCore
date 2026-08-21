namespace HomeMind.Business.IServices.Connector;

/// <summary>快递100 官方 MCP 客户端契约；实现不得持久化或回显完整运单号。</summary>
public interface IKuaidi100McpClient
{
    /// <summary>查询运单的最新状态事件。</summary>
    Task<Kuaidi100TrackingResult> TrackAsync(string trackingNumber, string? carrier, CancellationToken cancellationToken = default);
}

/// <summary>快递100 查询结果。</summary>
public sealed record Kuaidi100TrackingResult(IReadOnlyList<Kuaidi100TrackingEvent> Events);

/// <summary>快递100 状态事件。</summary>
public sealed record Kuaidi100TrackingEvent(string Status, string Description, string? Location, DateTime OccurredAt);
