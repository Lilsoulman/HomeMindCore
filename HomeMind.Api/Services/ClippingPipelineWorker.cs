using HomeMind.Business.IServices.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Api.Services;

/// <summary>剪辑四引擎后台消费者，避免在 API 请求线程启动本地引擎进程。</summary>
public sealed class ClippingPipelineWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    /// <summary>构造后台消费者。</summary>
    public ClippingPipelineWorker(IServiceScopeFactory scopes) => _scopes = scopes;
    /// <summary>按固定间隔处理一个持久化的剪辑任务。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using var scope = _scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IClippingPipelineServices>().ProcessNextAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
