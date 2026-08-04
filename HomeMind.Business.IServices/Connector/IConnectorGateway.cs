namespace HomeMind.Business.IServices.Connector;

/// <summary>Connector 是 Skill 访问所有外部系统的统一入口。</summary>
public interface IConnectorGateway
{
    Task<ConnectorInvocationResult> InvokeAsync(ConnectorInvocationRequest request, CancellationToken cancellationToken = default);
}

public sealed record ConnectorInvocationRequest(long AgentRunId, long UserId, long TenantId, long WorkspaceConnectorId, string ToolName, string InputJson, string IdempotencyKey);
public sealed record ConnectorInvocationResult(bool Succeeded, string Status, string? OutputJson, string? ErrorCode, string? Message);
