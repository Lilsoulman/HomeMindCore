using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Common.Repository;
using HomeMind.Common.Model.Entities.SmartHome;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace HomeMind.Api.Services;

/// <summary>Home Assistant 实时状态后台宿主，仅为已连接的家庭连接器维持可重订阅的 WebSocket 会话。</summary>
public sealed class HomeAssistantEventWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HomeAssistantEventWorker> _logger;

    /// <summary>构造实时状态后台宿主。</summary>
    public HomeAssistantEventWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<HomeAssistantEventWorker> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>在启用订阅时依次维护连接器会话；异常后短暂退避并重新从数据库读取连接状态。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("Mcp:Clients:HomeAssistant:EventSubscriptionEnabled")) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HomeMindDbContext>();
                var subscribers = scope.ServiceProvider.GetRequiredService<IHomeAssistantEventSubscriber>();
                var connectors = await (from connector in db.WorkspaceConnectors
                                        join provider in db.ConnectorProviders on connector.ConnectorProviderId equals provider.Id
                                        where connector.DeletedAt == null && connector.Status == "connected" && connector.AuthStatus == WorkspaceConnectorAuthStatus.Connected && provider.Code == "home_assistant"
                                        select connector).ToListAsync(stoppingToken);
                foreach (var connector in connectors) await subscribers.SubscribeAsync(connector, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception error)
            {
                _logger.LogWarning(error, "Home Assistant 状态订阅已断开，将重试");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
