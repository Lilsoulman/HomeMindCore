namespace HomeMind.Business.IServices.Skill;

/// <summary>Skill 是唯一允许产生外部副作用的执行边界。</summary>
public interface ISkillExecutor
{
    Task<SkillExecutionResult> ExecuteAsync(SkillExecutionRequest request, CancellationToken cancellationToken = default);
}

public sealed record SkillExecutionRequest(long AgentRunId, long UserId, long TenantId, string SkillCode, string InputJson, string IdempotencyKey);
public sealed record SkillExecutionResult(bool Succeeded, string Status, string? ResultJson, string? ErrorCode, string? Message);
