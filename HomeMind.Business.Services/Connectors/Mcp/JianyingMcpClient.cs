using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>
/// 剪映（jianying）MCP 客户端实现：经 <see cref="IMcpProcessClient"/>（本地 stdio jianying-mcp
/// 进程）按剪辑方案生成剪映 .draft 草稿内容（字节流）。流程：解析方案 JSON → 调用
/// <c>create_draft</c>（draft_name/width/height/fps）→ 按 MCP 返回的草稿目录读取 draft.json
/// 字节返回（SAVE_PATH 由 MCP 环境变量提供，本类仅在 MCP 返回路径下读取文件，不向调用方
/// 暴露草稿绝对路径）。素材片段装配与最终 draft.json 内容以部署的 jianying-mcp 版本工具
/// 契约为准（部署验证时校准）；草稿内容不可读时抛 <see cref="McpClientException"/>，
/// 由调用方按登记失败（502）处理。
/// </summary>
public sealed class JianyingMcpClient : IClippingMcpClient
{
    private const string DefaultDraftNamePrefix = "quick_edit";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMcpProcessClient _process;

    /// <summary>构造剪映 MCP 客户端。</summary>
    /// <param name="process">本地 stdio MCP 进程客户端（jianying-mcp）。</param>
    public JianyingMcpClient(IMcpProcessClient process) => _process = process;

    /// <inheritdoc />
    public async Task<byte[]> GenerateDraftAsync(string planJson, CancellationToken cancellationToken = default)
    {
        var draftName = BuildDraftName(planJson);
        var arguments = new JsonObject
        {
            ["draft_name"] = draftName,
            ["width"] = 1920,
            ["height"] = 1080,
            ["fps"] = 30
        };
        var result = await _process.CallToolAsync("create_draft", arguments, cancellationToken);
        var draftPath = ReadDraftPath(result);
        if (string.IsNullOrWhiteSpace(draftPath))
            throw new McpClientException("剪映 MCP 未返回草稿目录，无法读取草稿内容。");
        if (!File.Exists(draftPath))
            throw new McpClientException("剪映 MCP 返回的草稿文件不存在。");
        return await File.ReadAllBytesAsync(draftPath, cancellationToken);
    }

    /// <summary>从方案 JSON 提取展示名（素材位置最后一段），拼接草稿名称；解析失败回退为默认名称。</summary>
    private static string BuildDraftName(string planJson)
    {
        try
        {
            using var document = JsonDocument.Parse(planJson);
            if (ReadStringValue(document.RootElement, "media_location") is { } location && !string.IsNullOrWhiteSpace(location))
            {
                var trimmed = location.Trim().TrimEnd('/', '\\');
                var index = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
                var name = index >= 0 ? trimmed[(index + 1)..] : trimmed;
                if (!string.IsNullOrWhiteSpace(name)) return $"{DefaultDraftNamePrefix}_{name}";
            }
        }
        catch (JsonException)
        {
            // 解析失败使用默认名称。
        }
        return $"{DefaultDraftNamePrefix}_{Guid.NewGuid():N}";
    }

    /// <summary>从 JsonElement 读取字符串字段（蛇形/驼峰均可）；无匹配返回 null。</summary>
    private static string? ReadStringValue(JsonElement element, string snakeName)
    {
        if (element.TryGetProperty(snakeName, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>从 create_draft 返回中解析草稿 draft.json 完整路径；支持路径或目录 + 草稿名组合。</summary>
    private static string? ReadDraftPath(JsonNode? result)
    {
        if (result is not JsonObject root) return null;
        var draftPath = ReadString(root, "draft_path") ?? ReadString(root, "draftPath");
        if (!string.IsNullOrWhiteSpace(draftPath))
        {
            if (draftPath.EndsWith("draft.json", StringComparison.OrdinalIgnoreCase)) return draftPath;
            return Path.Combine(draftPath, "draft.json");
        }
        var draftDir = ReadString(root, "draft_dir") ?? ReadString(root, "draftDir") ?? ReadString(root, "save_path") ?? ReadString(root, "path");
        var draftId = ReadString(root, "draft_id") ?? ReadString(root, "draftId") ?? ReadString(root, "id");
        if (!string.IsNullOrWhiteSpace(draftDir) && !string.IsNullOrWhiteSpace(draftId))
            return Path.Combine(draftDir, draftId, "draft.json");
        return null;
    }

    /// <summary>按常见键名读取字符串字段（蛇形/驼峰均可）；无匹配返回 null。</summary>
    private static string? ReadString(JsonObject root, string snakeName)
    {
        if (root.TryGetPropertyValue(snakeName, out var value) && value is JsonValue stringValue && stringValue.TryGetValue<string>(out var text)) return text;
        var parts = snakeName.Split('_');
        if (parts.Length > 1)
        {
            var camelName = parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            if (root.TryGetPropertyValue(camelName, out value) && value is JsonValue camelValue && camelValue.TryGetValue<string>(out var camelText)) return camelText;
        }
        return null;
    }
}
