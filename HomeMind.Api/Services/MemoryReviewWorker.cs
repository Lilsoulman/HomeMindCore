using HomeMind.Business.IServices.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Api.Services;

/// <summary>Background consumer that turns explicit completed-Run proposals into pending memory reviews.</summary>
public sealed class MemoryReviewWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MemoryReviewWorker> _logger;

    /// <summary>Creates the worker.</summary>
    public MemoryReviewWorker(IServiceScopeFactory scopes, ILogger<MemoryReviewWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Memory review worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IMemoryReviewServices>().ProcessNextAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception error)
            {
                _logger.LogError(error, "Memory review worker iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        _logger.LogInformation("Memory review worker stopped");
    }
}
