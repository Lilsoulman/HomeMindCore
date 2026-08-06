namespace HomeMind.Common.Model.Agent;

/// <summary>AgentRun 的唯一允许状态集合。</summary>
public static class AgentRunStatus
{
    /// <summary>草稿态，已在数据库中创建但尚未入队。</summary>
    public const string Draft = "draft";
    /// <summary>已入队，等待智能体运行时调度。</summary>
    public const string Queued = "queued";
    /// <summary>规划阶段，正在生成动作草稿。</summary>
    public const string Planning = "planning";
    /// <summary>执行阶段，正在调用工具或执行动作。</summary>
    public const string Running = "running";
    /// <summary>已成功完成。</summary>
    public const string Completed = "completed";
    /// <summary>运行失败终止。</summary>
    public const string Failed = "failed";
    /// <summary>被用户或系统取消。</summary>
    public const string Cancelled = "cancelled";

    /// <summary>判断给定状态是否属于终态。</summary>
    /// <param name="status">待判断的运行状态字符串。</param>
    /// <returns>若状态为已完成、失败或取消则返回 true；否则返回 false。</returns>
    public static bool IsTerminal(string status) => status is Completed or Failed or Cancelled;
}
