using System.Text.Json;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors.Bridge;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>命令转发桥接定向测试：健康检查失败不转发命令、健康通过时转发并返回执行器结果。</summary>
public class CommandRelayServicesTests
{
    private static readonly ConnectorReference Reference = new(1, 1, "vault://ha");

    /// <summary>健康检查失败时不得调用命令执行器，并返回连接器错误。</summary>
    [Fact]
    public async Task Execute_Does_Not_Forward_When_Health_Check_Fails()
    {
        var relay = new CommandRelayService(
            [new FakeAdapter("ha", healthy: false, errorCode: "unreachable")],
            [new FakeExecutor("ha")]);

        var result = await relay.ExecuteAsync("ha", Reference, Command());

        Assert.False(result.Succeeded);
        Assert.Equal("unreachable", result.ErrorCode);
        Assert.Equal(0, FakeExecutor.Invocations);
    }

    /// <summary>健康检查通过时转发命令并返回执行器结果。</summary>
    [Fact]
    public async Task Execute_Forwards_Command_When_Health_Check_Passes()
    {
        var relay = new CommandRelayService(
            [new FakeAdapter("ha", healthy: true)],
            [new FakeExecutor("ha")]);

        var result = await relay.ExecuteAsync("ha", Reference, Command());

        Assert.True(result.Succeeded);
        Assert.Equal("executed", result.Status);
        Assert.Equal(1, FakeExecutor.Invocations);
    }

    /// <summary>未注册的 Provider 不转发命令，返回适配器不可用错误。</summary>
    [Fact]
    public async Task Execute_Returns_Adapter_Unavailable_For_Unknown_Provider()
    {
        var relay = new CommandRelayService(
            [new FakeAdapter("ha", healthy: true)],
            [new FakeExecutor("ha")]);

        var result = await relay.ExecuteAsync("mqtt", Reference, Command());

        Assert.False(result.Succeeded);
        Assert.Equal("adapter_unavailable", result.ErrorCode);
        Assert.Equal(0, FakeExecutor.Invocations);
    }

    private static DeviceCommand Command() =>
        new(1, 10, "power", JsonDocument.Parse("true").RootElement.Clone(), 7, 99, Guid.NewGuid().ToString());

    private sealed class FakeAdapter : IDeviceAdapter
    {
        public FakeAdapter(string providerCode, bool healthy, string? errorCode = null)
        {
            ProviderCode = providerCode;
            _healthy = healthy;
            _errorCode = errorCode;
        }

        private readonly bool _healthy;
        private readonly string? _errorCode;
        public string ProviderCode { get; }

        public Task<ConnectorConnectionTestResult> TestConnectionAsync(ConnectorReference connector, CancellationToken cancellationToken = default) =>
            Task.FromResult(_healthy
                ? new ConnectorConnectionTestResult(true, Message: "ok")
                : new ConnectorConnectionTestResult(false, _errorCode, "连接失败。"));

        public Task<AdapterDeviceState?> ReadDeviceStateAsync(ConnectorReference connector, long deviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdapterDeviceState?>(null);
    }

    private sealed class FakeExecutor : IDeviceCommandExecutor
    {
        public FakeExecutor(string providerCode) => ProviderCode = providerCode;

        public static int Invocations { get; private set; }

        public string ProviderCode { get; }

        public Task<DeviceCommandResult> ExecuteCommandAsync(ConnectorReference connector, DeviceCommand command, CancellationToken cancellationToken = default)
        {
            Invocations++;
            return Task.FromResult(new DeviceCommandResult(true, "executed", Message: "已执行。"));
        }
    }
}
