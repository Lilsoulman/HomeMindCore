using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Bridge;

/// <summary>
/// 命令转发桥接服务。仅转发已经授权、确认并具备幂等键的设备命令；
/// 转发前先做连接器健康检查，健康检查失败不转发命令。业务层与 Controller 不感知任何具体厂商实现。
/// </summary>
public sealed class CommandRelayService
{
    private readonly IReadOnlyDictionary<string, IDeviceAdapter> _adapters;
    private readonly IReadOnlyDictionary<string, IDeviceCommandExecutor> _executors;

    /// <summary>构造命令转发桥接服务。</summary>
    /// <param name="adapters">全部设备适配器，按 ProviderCode 索引，用于健康检查。</param>
    /// <param name="executors">全部命令执行器，按 ProviderCode 索引。</param>
    public CommandRelayService(IEnumerable<IDeviceAdapter> adapters, IEnumerable<IDeviceCommandExecutor> executors)
    {
        _adapters = adapters.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _executors = executors.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>当前连接器是否具备健康检查适配器。</summary>
    /// <param name="providerCode">连接器 Provider 编码。</param>
    /// <returns>具备时为 true。</returns>
    public bool SupportsProvider(string providerCode) => _adapters.ContainsKey(providerCode) && _executors.ContainsKey(providerCode);

    /// <summary>执行连接器健康检查。</summary>
    /// <param name="providerCode">连接器 Provider 编码。</param>
    /// <param name="reference">连接器引用（含凭据引用）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>连接测试结果。</returns>
    public Task<ConnectorConnectionTestResult> TestConnectionAsync(string providerCode, ConnectorReference reference, CancellationToken cancellationToken = default) =>
        _adapters.TryGetValue(providerCode, out var adapter)
            ? adapter.TestConnectionAsync(reference, cancellationToken)
            : Task.FromResult(new ConnectorConnectionTestResult(false, "adapter_unavailable", "该连接器尚未提供运行期适配器。"));

    /// <summary>健康检查通过后转发设备命令；健康检查失败返回连接器错误且不转发。</summary>
    /// <param name="providerCode">连接器 Provider 编码。</param>
    /// <param name="reference">连接器引用（含凭据引用）。</param>
    /// <param name="command">已经授权、确认并具备幂等键的设备命令。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命令执行结果；健康检查失败时 Succeeded 为 false。</returns>
    public async Task<DeviceCommandResult> ExecuteAsync(string providerCode, ConnectorReference reference, DeviceCommand command, CancellationToken cancellationToken = default)
    {
        if (!_executors.TryGetValue(providerCode, out var executor))
            return new DeviceCommandResult(false, "failed", "adapter_unavailable", "该连接器尚未提供设备行动适配器。");

        var health = await TestConnectionAsync(providerCode, reference, cancellationToken);
        if (!health.Succeeded)
            return new DeviceCommandResult(false, "failed", health.ErrorCode ?? "connector_unavailable", health.Message ?? "连接器健康检查失败。");
        return await executor.ExecuteCommandAsync(reference, command, cancellationToken);
    }
}
