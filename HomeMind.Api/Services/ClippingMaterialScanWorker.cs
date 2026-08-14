using HomeMind.Business.IServices.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HomeMind.Api.Services;

/// <summary>素材自动发现后台轮询：按配置间隔扫描素材根目录并登记新文件，避免在 API 请求线程执行文件系统遍历。</summary>
public sealed class ClippingMaterialScanWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ClippingMaterialScanWorker> _logger;
    private readonly int _intervalSeconds;

    /// <summary>构造素材自动发现后台轮询。</summary>
    /// <param name="scopes">服务作用域工厂，供每次轮询独立作用域。</param>
    /// <param name="configuration">应用配置，读取 Clipping:Scan:IntervalSeconds（默认 60 秒）。</param>
    /// <param name="logger">日志器。</param>
    public ClippingMaterialScanWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ClippingMaterialScanWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _intervalSeconds = Math.Max(1, configuration.GetValue<int?>("Clipping:Scan:IntervalSeconds") ?? 60);
    }

    /// <summary>按固定间隔执行一轮素材自动发现；单轮异常不影响后续轮询。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("素材自动发现后台轮询已启动，间隔 {IntervalSeconds} 秒", _intervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IClippingMaterialScanServices>().ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception error)
            {
                // 单轮失败不中断进程，等待下一轮重试。
                _logger.LogError(error, "素材自动发现单轮失败，等待下一轮重试");
            }
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
        _logger.LogInformation("素材自动发现后台轮询已停止");
    }
}
