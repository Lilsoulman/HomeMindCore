namespace HomeMind.Business.IServices.Memory;

/// <summary>为 Agent 提供租户隔离的上下文读取边界；长期记忆持久化后续实现。</summary>
public interface IAgentMemoryServices
{
    Task<AgentMemoryContext> ReadAsync(long userId, long tenantId, string query, CancellationToken cancellationToken = default);
}

public sealed record AgentMemoryContext(string ContextJson, DateTime ReadAt);
