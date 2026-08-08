namespace HomeMind.Business.IServices.Connector;

/// <summary>本地 stdio MCP 客户端调用异常：进程不可用、JSON-RPC 错误或工具执行失败时抛出；消息面向运维诊断，不包含 MCP 内部路径。</summary>
public sealed class McpClientException : Exception
{
    /// <summary>构造 MCP 客户端异常。</summary>
    /// <param name="message">面向运维的诊断信息。</param>
    /// <param name="innerException">底层异常（进程启动/IO 失败），可为 null。</param>
    public McpClientException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
