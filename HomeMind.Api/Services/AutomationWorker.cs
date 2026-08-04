using HomeMind.Business.IServices.SmartHome;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Api.Services;

/// <summary>In-process execution host for durable sync work and time-based automation checks.</summary>
public sealed class AutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConnectorSyncQueue _queue;
    private readonly ILogger<AutomationWorker> _logger;

    public AutomationWorker(IServiceScopeFactory scopes, IConnectorSyncQueue queue, ILogger<AutomationWorker> logger)
    {
        _scopes = scopes;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Automation background worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var runtime = scope.ServiceProvider.GetRequiredService<IConnectorRuntimeServices>();
                var automation = scope.ServiceProvider.GetRequiredService<IAutomationRuleServices>();
                while (_queue.TryDequeue(out var jobId)) await runtime.ProcessSyncJobAsync(jobId, stoppingToken);
                await runtime.ProcessDueSyncJobsAsync(stoppingToken);
                var triggered = await automation.ProcessDueSchedulesAsync(DateTime.UtcNow, stoppingToken);
                if (triggered > 0) _logger.LogInformation("Automation schedule scan triggered {TriggeredRuleCount} rules", triggered);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception error)
            {
                _logger.LogError(error, "Automation background worker iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        _logger.LogInformation("Automation background worker stopped");
    }
}
