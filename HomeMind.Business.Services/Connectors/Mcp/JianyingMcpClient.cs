using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>
/// 剪映（jianying）MCP 客户端实现：经 <see cref="IMcpProcessClient"/>（本地 stdio jianying-mcp
/// 进程）按剪辑方案生成剪映草稿。流程：解析方案 JSON → create_draft → create_track →
/// add_video_segment（装配素材片段）→ export_draft（直落剪映草稿箱，产出 draft_content.json +
/// draft_meta_info.json）→ 读取 draft_content.json 字节返回（留档登记）。SAVE_PATH/OUTPUT_PATH
/// 由部署目录 .env 提供（WorkingDirectory 加载）。工具契约经 2026-08-09 部署校准：create_draft
/// 仅返回 draft_id（不返回路径），create_track/add_video_segment/export_draft 返回
/// {success,message,data}；素材以绝对路径透传（export 不复制素材进草稿，剪映本机可打开）。
/// 任一步失败抛 <see cref="McpClientException"/>，由调用方按登记失败（502）处理。
/// </summary>
public sealed class JianyingMcpClient : IClippingMcpClient
{
    private const string DefaultDraftNamePrefix = "quick_edit";
    private const int DefaultDurationSeconds = 15;
    private const int DraftWidth = 1920;
    private const int DraftHeight = 1080;
    private const int DraftFps = 30;
    private const string VideoTrackName = "video1";

    private readonly IMcpProcessClient _process;

    /// <summary>构造剪映 MCP 客户端。</summary>
    /// <param name="process">本地 stdio MCP 进程客户端（jianying-mcp）。</param>
    public JianyingMcpClient(IMcpProcessClient process) => _process = process;

    /// <inheritdoc />
    public async Task<byte[]> GenerateDraftAsync(string planJson, CancellationToken cancellationToken = default)
    {
        var draftName = BuildDraftName(planJson);
        var materialPath = Path.GetFullPath(ReadMaterialPath(planJson));
        var durationSeconds = ReadDurationSeconds(planJson);

        var draftId = await CreateDraftAsync(draftName, cancellationToken);
        var trackId = await CreateTrackAsync(draftId, cancellationToken);
        await AddVideoSegmentAsync(trackId, materialPath, durationSeconds, cancellationToken);
        var outputPath = await ExportDraftAsync(draftId, cancellationToken);

        var draftContentPath = Path.Combine(outputPath, "draft_content.json");
        if (!File.Exists(draftContentPath))
            throw new McpClientException("剪映 MCP 导出的草稿文件不存在。");
        return await File.ReadAllBytesAsync(draftContentPath, cancellationToken);
    }

    /// <summary>调用 create_draft 创建草稿，返回草稿 ID；未返回时抛异常。</summary>
    private async Task<string> CreateDraftAsync(string draftName, CancellationToken cancellationToken)
    {
        var result = await _process.CallToolAsync("create_draft", new JsonObject
        {
            ["draft_name"] = draftName,
            ["width"] = DraftWidth,
            ["height"] = DraftHeight,
            ["fps"] = DraftFps
        }, cancellationToken);
        var draftId = ReadDataString(result, "draft_id");
        if (string.IsNullOrWhiteSpace(draftId))
            throw new McpClientException("剪映 MCP create_draft 未返回草稿 ID。");
        return draftId;
    }

    /// <summary>调用 create_track 创建视频轨道，返回轨道 ID；未返回时抛异常。</summary>
    private async Task<string> CreateTrackAsync(string draftId, CancellationToken cancellationToken)
    {
        var result = await _process.CallToolAsync("create_track", new JsonObject
        {
            ["draft_id"] = draftId,
            ["track_type"] = "video",
            ["track_name"] = VideoTrackName
        }, cancellationToken);
        var trackId = ReadDataString(result, "track_id");
        if (string.IsNullOrWhiteSpace(trackId))
            throw new McpClientException($"剪映 MCP create_track 未返回轨道 ID：{ReadDataString(result, "message")}");
        return trackId;
    }

    /// <summary>调用 add_video_segment 装配素材片段到轨道；失败时抛异常。</summary>
    /// <remarks>部署校准 2026-08-10：jianying-mcp 契约要求 target 轨道时长不得超出素材本身时长（否则
    /// 返回「参数错误: 素材所占的轨道时长…超出素材本身时长…」），而方案时长来自用户指令（1-600 秒）
    /// 不感知素材实际时长——装配前经 parse_media_info 探测素材时长并截断 target；探测失败（媒体解析
    /// 异常）回退方案时长（原行为，后续由调用方按失败处理）。</remarks>
    private async Task AddVideoSegmentAsync(string trackId, string materialPath, int durationSeconds, CancellationToken cancellationToken)
    {
        var targetSeconds = durationSeconds;
        var materialDuration = await ProbeMaterialDurationAsync(materialPath, cancellationToken);
        if (materialDuration is > 0 && materialDuration < targetSeconds)
            targetSeconds = (int)Math.Floor(materialDuration.Value);
        var result = await _process.CallToolAsync("add_video_segment", new JsonObject
        {
            ["track_id"] = trackId,
            ["material"] = materialPath,
            ["target_start_end"] = $"0s-{targetSeconds}s"
        }, cancellationToken);
        if (!ReadDataBool(result, "success", defaultValue: true))
            throw new McpClientException($"剪映 MCP add_video_segment 添加素材失败：{ReadDataString(result, "message")}");
    }

    /// <summary>调用 parse_media_info 探测素材实际时长（秒）；解析失败返回 null 不阻断主流程。</summary>
    private async Task<double?> ProbeMaterialDurationAsync(string materialPath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _process.CallToolAsync("parse_media_info", new JsonObject { ["media_path"] = materialPath }, cancellationToken);
            if (result is not JsonObject root) return null;
            if (root.TryGetPropertyValue("data", out var data) && data is JsonObject dataObject)
            {
                if (dataObject.TryGetPropertyValue("media_info", out var info) && info is JsonObject mediaInfo &&
                    TryReadNumber(mediaInfo, "duration", out var nested)) return nested;
                if (TryReadNumber(dataObject, "duration", out var direct)) return direct;
            }
            if (TryReadNumber(root, "duration", out var top)) return top;
        }
        catch (McpClientException)
        {
            // 素材探测失败（媒体解析异常等）不阻断装配，回退方案时长。
        }
        return null;
    }

    /// <summary>从 JsonObject 读取数值字段（蛇形/驼峰均可）；无匹配返回 false。</summary>
    private static bool TryReadNumber(JsonObject root, string snakeName, out double value)
    {
        if (root.TryGetPropertyValue(snakeName, out var node) && node is JsonValue number && number.TryGetValue<double>(out value)) return true;
        var parts = snakeName.Split('_');
        if (parts.Length > 1)
        {
            var camelName = parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            if (root.TryGetPropertyValue(camelName, out node) && node is JsonValue camelNumber && camelNumber.TryGetValue<double>(out value)) return true;
        }
        value = 0;
        return false;
    }

    /// <summary>调用 export_draft 导出剪映草稿，返回草稿目录路径；未返回时抛异常。</summary>
    private async Task<string> ExportDraftAsync(string draftId, CancellationToken cancellationToken)
    {
        var result = await _process.CallToolAsync("export_draft", new JsonObject
        {
            ["draft_id"] = draftId
        }, cancellationToken);
        var outputPath = ReadDataString(result, "output_path");
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new McpClientException($"剪映 MCP export_draft 未返回草稿目录：{ReadDataString(result, "message")}");
        return outputPath;
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

    /// <summary>从方案 JSON 读取素材位置；缺失或解析失败抛异常。</summary>
    private static string ReadMaterialPath(string planJson)
    {
        try
        {
            using var document = JsonDocument.Parse(planJson);
            if (ReadStringValue(document.RootElement, "media_location") is { Length: > 0 } location)
                return location;
        }
        catch (JsonException)
        {
            // 落到下方统一异常。
        }
        throw new McpClientException("剪辑方案缺少素材位置 media_location。");
    }

    /// <summary>从方案 JSON 读取片段时长（秒）：segments[0].duration 或 total_duration；缺省 15 秒。</summary>
    private static int ReadDurationSeconds(string planJson)
    {
        try
        {
            using var document = JsonDocument.Parse(planJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (ReadNumberValue(document.RootElement, "total_duration") is { } total && total > 0)
                    return ToSeconds(total);
                if (document.RootElement.TryGetProperty("segments", out var segments) &&
                    segments.ValueKind == JsonValueKind.Array &&
                    segments.GetArrayLength() > 0 &&
                    segments[0].ValueKind == JsonValueKind.Object &&
                    ReadNumberValue(segments[0], "duration") is { } duration && duration > 0)
                    return ToSeconds(duration);
            }
        }
        catch (JsonException)
        {
            // 解析失败使用默认时长。
        }
        return DefaultDurationSeconds;
    }

    /// <summary>将数值时长向上取整为整数秒。</summary>
    private static int ToSeconds(double value) => (int)Math.Ceiling(value);

    /// <summary>从工具返回中读取字符串字段：兼容裸 dict（如 create_draft 的 draft_id）与 ToolResponse（data 下字段）；蛇形/驼峰均可。</summary>
    private static string? ReadDataString(JsonNode? result, string snakeName)
    {
        if (result is not JsonObject root) return null;
        var value = ReadString(root, snakeName);
        if (value is not null) return value;
        return root.TryGetPropertyValue("data", out var data) && data is JsonObject dataObject ? ReadString(dataObject, snakeName) : null;
    }

    /// <summary>从工具返回中读取布尔字段（ToolResponse success）；裸 dict 无该字段时返回 defaultValue。</summary>
    private static bool ReadDataBool(JsonNode? result, string snakeName, bool defaultValue)
    {
        if (result is not JsonObject root) return defaultValue;
        if (ReadBool(root, snakeName) is { } value) return value;
        return root.TryGetPropertyValue("data", out var data) && data is JsonObject dataObject && ReadBool(dataObject, snakeName) is { } dataValue
            ? dataValue
            : defaultValue;
    }

    /// <summary>从 JsonObject 按常见键名读取字符串字段（蛇形/驼峰均可）；无匹配返回 null。</summary>
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

    /// <summary>从 JsonObject 读取布尔字段（蛇形/驼峰均可）；无匹配返回 null。</summary>
    private static bool? ReadBool(JsonObject root, string snakeName)
    {
        if (root.TryGetPropertyValue(snakeName, out var value) && value is JsonValue boolValue && boolValue.TryGetValue<bool>(out var flag)) return flag;
        var parts = snakeName.Split('_');
        if (parts.Length > 1)
        {
            var camelName = parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            if (root.TryGetPropertyValue(camelName, out value) && value is JsonValue camelBool && camelBool.TryGetValue<bool>(out var camelFlag)) return camelFlag;
        }
        return null;
    }

    /// <summary>从 JsonElement 读取字符串字段（蛇形/驼峰均可）；无匹配返回 null。</summary>
    private static string? ReadStringValue(JsonElement element, string snakeName)
    {
        if (element.TryGetProperty(snakeName, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>从 JsonElement 读取数值字段（蛇形/驼峰均可）；无匹配返回 null。</summary>
    private static double? ReadNumberValue(JsonElement element, string snakeName)
    {
        if (element.TryGetProperty(snakeName, out var value) && value.ValueKind == JsonValueKind.Number) return value.GetDouble();
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }
}
