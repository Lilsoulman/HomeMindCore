using HomeMind.Common.Model.Entities.SmartHome;

namespace HomeMind.Business.IServices.SmartHome;

/// <summary>Home Assistant 实时事件订阅边界，仅将已过滤的状态变化交给标准化同步服务。</summary>
public interface IHomeAssistantEventSubscriber
{
    /// <summary>订阅单个已连接 Home Assistant 连接器的状态变化，断线时由调用方重新发起。</summary>
    Task SubscribeAsync(WorkspaceConnector connector, CancellationToken cancellationToken);
}
