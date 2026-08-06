using HomeMind.Business.IServices.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Api.Services;

/// <summary>In-process consumer for queued AgentRun jobs. Model calls never run on API request threads.</summary>
public sealed class AgentRuntimeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AgentRuntimeWorker> _logger;

    public AgentRuntimeWorker(IServiceScopeFactory scopes, ILogger<AgentRuntimeWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent runtime worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAgentRunProcessor>();
                await processor.ProcessNextAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception error)
            {
                _logger.LogError(error, "Agent runtime worker iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        _logger.LogInformation("Agent runtime worker stopped");
    }
}
