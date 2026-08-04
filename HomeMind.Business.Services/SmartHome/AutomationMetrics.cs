using System.Diagnostics.Metrics;

namespace HomeMind.Business.Services.SmartHome;

public static class AutomationMetrics
{
    public static readonly Meter Meter = new("HomeMind.Automation", "1.0");
    public static readonly Counter<long> RuleTriggered = Meter.CreateCounter<long>("automation_rules_triggered_total");
    public static readonly Counter<long> SyncQueued = Meter.CreateCounter<long>("connector_sync_queued_total");
    public static readonly Counter<long> SyncRetried = Meter.CreateCounter<long>("connector_sync_retried_total");
    public static readonly Counter<long> SyncFailed = Meter.CreateCounter<long>("connector_sync_failed_total");
    public static readonly Counter<long> TeamRunTriggered = Meter.CreateCounter<long>("team_runs_triggered_total");
    public static readonly Counter<long> TeamRunMemberFailed = Meter.CreateCounter<long>("team_run_members_failed_total");
    public static readonly Counter<long> TeamRunSynthesisFailed = Meter.CreateCounter<long>("team_run_synthesis_failed_total");
}
