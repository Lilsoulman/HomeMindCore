using System.Text.Json;

namespace HomeMind.Business.IServices.Connector;

/// <summary>设备命令；由已授权、已确认并具备幂等键的运行产生，绝不包含凭据。</summary>
/// <param name="ConnectorId">连接器主键。</param>
/// <param name="DeviceId">标准化设备主键。</param>
/// <param name="Capability">能力编码，如 power / brightness。</param>
/// <param name="TargetValue">目标值 JSON。</param>
/// <param name="OperatorUserId">发起确认的用户主键。</param>
/// <param name="RunActionId">来源运行动作主键，用于审计追溯。</param>
/// <param name="IdempotencyKey">幂等键，避免重复副作用。</param>
public sealed record DeviceCommand(long ConnectorId, long DeviceId, string Capability, JsonElement TargetValue, long OperatorUserId, long RunActionId, string IdempotencyKey);

/// <summary>设备命令执行结果。</summary>
/// <param name="Succeeded">命令是否执行成功。</param>
/// <param name="Status">执行状态，executed / failed。</param>
/// <param name="ErrorCode">失败错误码。</param>
/// <param name="Message">面向用户的中文说明。</param>
public sealed record DeviceCommandResult(bool Succeeded, string Status, string? ErrorCode = null, string? Message = null, string? StateJson = null);

/// <summary>设备命令执行契约。业务层只依赖本接口，不感知具体厂商实现。</summary>
public interface IDeviceCommandExecutor
{
    /// <summary>命令执行器对应的 Provider 编码，如 home_assistant。</summary>
    string ProviderCode { get; }

    /// <summary>执行一条已授权、已确认并具备幂等键的设备命令。</summary>
    /// <param name="connector">连接器引用。</param>
    /// <param name="command">设备命令。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命令执行结果。</returns>
    /// <exception cref="ConnectorAdapterException">连接失败或超时时抛出。</exception>
    Task<DeviceCommandResult> ExecuteCommandAsync(ConnectorReference connector, DeviceCommand command, CancellationToken cancellationToken = default);
}
