using System.Text.Json;
using System.Text.Json.Nodes;

namespace HomeMind.CreatorMcp;

internal static class Program
{
    private const string ProtocolVersion = "2025-03-26";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task Main()
    {
        // MCP 客户端经 stdio 管道以 UTF-8 收发 JSON-RPC；Windows 下若沿用系统代码页会把中文参数解码为乱码，必须显式固定编码。
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var options = CreatorCenterOptions.FromEnvironment();
        var store = new CreatorStore(options.DatabasePath);
        await store.InitializeAsync(CancellationToken.None);

        while (await Console.In.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonObject? request;
            try { request = JsonNode.Parse(line) as JsonObject; }
            catch (JsonException) { await WriteAsync(Error(null, -32700, "请求不是有效 JSON。")); continue; }
            if (request is null) { await WriteAsync(Error(null, -32600, "请求必须为 JSON 对象。")); continue; }

            var response = await HandleAsync(request, store, options);
            if (response is not null) await WriteAsync(response);
        }
    }

    private static async Task<JsonObject?> HandleAsync(JsonObject request, CreatorStore store, CreatorCenterOptions options)
    {
        var method = request["method"]?.GetValue<string>();
        var id = request["id"]?.DeepClone();
        if (id is null) return null;

        try
        {
            return method switch
            {
                "initialize" => Success(id, new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { tools = new { listChanged = false } },
                    serverInfo = new { name = "nexusmind-creator-mcp", version = "1.0.0" },
                    instructions = "Use the creator center tools to search locally synchronized expert, group, and skill metadata. Run sync_creator_center explicitly when freshness is required."
                }),
                "ping" => Success(id, new { }),
                "tools/list" => Success(id, new { tools = ToolDefinitions }),
                "tools/call" => Success(id, await CallToolAsync(request["params"] as JsonObject, store, options)),
                _ => Error(id, -32601, $"不支持 MCP 方法：{method ?? "(空)"}。")
            };
        }
        catch (Exception exception)
        {
            return Error(id, -32603, exception.Message);
        }
    }

    private static async Task<object> CallToolAsync(JsonObject? parameters, CreatorStore store, CreatorCenterOptions options)
    {
        var name = parameters?["name"]?.GetValue<string>();
        var arguments = parameters?["arguments"] as JsonObject ?? [];
        try
        {
            object result = name switch
            {
                "sync_creator_center" => await SyncAsync(arguments, store, options),
                "search_creator_center" => await SearchAsync(arguments, store),
                "get_creator_item" => await GetItemAsync(arguments, store, options),
                "creator_sync_status" => await store.GetStatusAsync(CancellationToken.None),
                _ => throw new ArgumentException($"不存在的工具：{name ?? "(空)"}。")
            };
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, JsonOptions) } } };
        }
        catch (Exception exception)
        {
            return new { content = new[] { new { type = "text", text = exception.Message } }, isError = true };
        }
    }

    private static async Task<object> SyncAsync(JsonObject arguments, CreatorStore store, CreatorCenterOptions options)
    {
        var includeSensitive = arguments["includeSensitiveData"]?.GetValue<bool>() ?? false;
        if (includeSensitive && !options.AllowSensitiveData)
        {
            throw new InvalidOperationException("敏感数据同步已禁用。若确有需要，请设置 NEXUSMIND_MCP_ALLOW_SENSITIVE=true 后重启 MCP 服务。");
        }

        var client = new CreatorCenterClient(options);
        var items = await client.FetchAsync(includeSensitive, CancellationToken.None);
        return await store.ReplaceAsync(items, CancellationToken.None);
    }

    private static Task<IReadOnlyList<CreatorItemSummary>> SearchAsync(JsonObject arguments, CreatorStore store) =>
        store.SearchAsync(arguments["query"]?.GetValue<string>(), arguments["type"]?.GetValue<string>(), arguments["category"]?.GetValue<string>(), arguments["limit"]?.GetValue<int>() ?? 20, CancellationToken.None);

    private static async Task<object> GetItemAsync(JsonObject arguments, CreatorStore store, CreatorCenterOptions options)
    {
        var type = arguments["type"]?.GetValue<string>();
        var id = arguments["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id)) throw new ArgumentException("type 和 id 均为必填项。");
        var item = await store.GetAsync(type, id, CancellationToken.None) ?? throw new InvalidOperationException("本地数据库中不存在该创作者中心项目，请先同步。");
        var includeSensitive = arguments["includeSensitiveData"]?.GetValue<bool>() ?? false;
        if (includeSensitive && (!options.AllowSensitiveData || !item.ContainsSensitiveData))
        {
            throw new InvalidOperationException("敏感内容未同步或 MCP 服务未启用敏感数据访问。");
        }

        var payload = JsonNode.Parse(item.PayloadJson);
        if (!includeSensitive && payload is JsonObject payloadObject)
        {
            payloadObject.Remove("PromptTemplate");
            payloadObject.Remove("promptTemplate");
            payloadObject.Remove("Prompt");
            payloadObject.Remove("prompt");
        }
        return new { item.Type, item.Id, item.Code, item.Name, item.Category, item.Description, payload, item.ContainsSensitiveData };
    }

    private static JsonObject Success(JsonNode id, object result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = JsonSerializer.SerializeToNode(result, JsonOptions) };
    private static JsonObject Error(JsonNode? id, int code, string message) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };
    private static Task WriteAsync(JsonObject response) => Console.Out.WriteLineAsync(response.ToJsonString(JsonOptions));

    private static readonly object[] ToolDefinitions =
    [
        new { name = "sync_creator_center", description = "从已认证的 NexusMind 创作者中心同步专家、专家组和技能元数据到本地 SQLite。默认不保存敏感提示词。", inputSchema = new { type = "object", properties = new { includeSensitiveData = new { type = "boolean", description = "仅当服务器显式允许时，保存专家提示词和技能提示词。" } } } },
        new { name = "search_creator_center", description = "在本地 SQLite 中搜索已同步的专家、专家组和技能；返回安全摘要，不返回提示词。", inputSchema = new { type = "object", properties = new { query = new { type = "string" }, type = new { type = "string", @enum = new[] { "expert", "group", "skill" } }, category = new { type = "string" }, limit = new { type = "integer", minimum = 1, maximum = 100 } } } },
        new { name = "get_creator_item", description = "读取一项已同步创作者中心数据的完整本地元数据。敏感字段必须经服务器显式允许。", inputSchema = new { type = "object", required = new[] { "type", "id" }, properties = new { type = new { type = "string", @enum = new[] { "expert", "group", "skill" } }, id = new { type = "string" }, includeSensitiveData = new { type = "boolean" } } } },
        new { name = "creator_sync_status", description = "查询本地创作者中心 SQLite 数据库的最后成功同步时间和项目计数。", inputSchema = new { type = "object", properties = new { } } }
    ];
}
