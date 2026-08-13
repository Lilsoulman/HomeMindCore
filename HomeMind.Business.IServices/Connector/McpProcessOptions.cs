namespace HomeMind.Business.IServices.Connector;

/// <summary>本地 stdio MCP 进程客户端配置项：进程启动命令与单次工具调用超时。</summary>
public sealed class McpProcessOptions
{
    /// <summary>可执行文件路径或命令名（如 npx），随 Arguments 组成完整启动命令。</summary>
    public string CommandFileName { get; init; } = "";

    /// <summary>命令行参数（如 "xhs-mcp mcp"），与 CommandFileName 拼接启动。</summary>
    public string Arguments { get; init; } = "";

    /// <summary>进程工作目录（可空）；xhs-mcp 等本地 MCP 需在其部署目录下运行以命中本地数据与性能基线。</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>单次工具调用（含进程启动与握手）超时秒数，默认 30 秒。</summary>
    public int TimeoutSeconds { get; init; } = 30;
}
