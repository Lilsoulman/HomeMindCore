using System.Text.Json.Nodes;

namespace HomeMind.Business.IServices.Connector;

/// <summary>
/// 本地 stdio MCP 进程客户端契约：以 JSON-RPC 2.0（换行分隔帧）经标准输入/输出与本地
/// MCP Server 进程（如 xhs-mcp、jianying-mcp）通信，遵守本地优先原则——进程仅运行于
/// 后端所在主机，不直连远端服务，不暴露 MCP 内部路径或凭据。实现必须保证：懒启动与
/// initialize 握手、请求-响应按 id 关联、可配置超时、进程异常可诊断；单个工具调用串行执行。
/// </summary>
public interface IMcpProcessClient
{
    /// <summary>调用 MCP Server 的 <c>tools/call</c>，返回工具 result.content 首个 text 条目解析后的 JSON 节点。</summary>
    /// <param name="toolName">工具名称（如 xhs_search_note、create_draft）。</param>
    /// <param name="arguments">工具参数 JSON 对象；可为 null 表示无参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具返回的文本内容解析后的 JSON 节点；无文本内容返回 null。</returns>
    /// <exception cref="McpClientException">进程不可用、MCP 错误或工具执行失败时抛出。</exception>
    Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default);

    /// <summary>停止并释放 MCP 进程；幂等，可安全重复调用。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
