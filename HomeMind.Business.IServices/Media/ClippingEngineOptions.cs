namespace HomeMind.Business.IServices.Media;

/// <summary>剪辑四引擎的受控本地进程配置。</summary>
public sealed class ClippingEngineOptions
{
    /// <summary>是否允许该引擎执行；默认关闭。</summary>
    public bool Enabled { get; init; }
    /// <summary>受控命令文件名。</summary>
    public string CommandFileName { get; init; } = "";
    /// <summary>受控命令参数。</summary>
    public string Arguments { get; init; } = "";
    /// <summary>受控进程工作目录。</summary>
    public string? WorkingDirectory { get; init; }
    /// <summary>执行超时秒数。</summary>
    public int TimeoutSeconds { get; init; } = 60;
    /// <summary>健康检查参数；为空时仅验证启用与命令配置。</summary>
    public string? HealthCheckArguments { get; init; }
    /// <summary>经部署核验的引擎版本或提交标识。</summary>
    public string? Version { get; init; }
    /// <summary>Seedance 服务端密钥是否已经安全注入，仅供服务端判断。</summary>
    public string? ApiKey { get; init; }
}
