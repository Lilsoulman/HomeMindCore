namespace HomeMind.Business.IServices.Agent;

/// <summary>AgentRun 作业处理器：消费 queued 的 ExpertJob，调用 LLM 并回写运行结果。</summary>
public interface IAgentRunProcessor
{
    /// <summary>处理下一个排队中的作业。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次处理的作业数量（0 表示当前无排队作业）。</returns>
    Task<int> ProcessNextAsync(CancellationToken cancellationToken = default);
}
