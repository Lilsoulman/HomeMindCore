using HomeMind.Business.IServices.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Api.Services;

/// <summary>剪辑四引擎后台消费者，避免在 API 请求线程启动本地引擎进程。</summary>
public sealed class ClippingPipelineWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ClippingPipelineWorker> _logger;
    /// <summary>构造后台消费者。</summary>
    public ClippingPipelineWorker(IServiceScopeFactory scopes, ILogger<ClippingPipelineWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }
    /// <summary>按固定间隔处理一个持久化的剪辑任务。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using var scope = _scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IClippingPipelineServices>().ProcessNextAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "剪辑流水线后台任务执行失败，将在下一轮重试。请检查 IClippingPipelineServices 及其依赖是否已注册。");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }
}
