namespace HomeMind.Common.Model.Agent;

/// <summary>AgentRun 的唯一允许状态集合。</summary>
public static class AgentRunStatus
{
    public const string Draft = "draft";
    public const string Queued = "queued";
    public const string Planning = "planning";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static bool IsTerminal(string status) => status is Completed or Failed or Cancelled;
}
