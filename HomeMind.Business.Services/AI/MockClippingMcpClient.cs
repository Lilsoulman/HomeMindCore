using System.Text;
using System.Text.Json;
using HomeMind.Business.IServices.AI;

namespace HomeMind.Business.Services.AI;

/// <summary>
/// 剪辑 MCP 的确定性 Mock 实现：按剪辑方案生成最小剪映草稿 JSON 内容（片段序列/总时长/模拟
/// draft_roaming_id），不访问素材目录、不产生真实文件路径。B25 基线使用；真实
/// jianying-mcp / capcut-mate 接入为部署环境验证项（需可访问素材与剪映草稿目录的主机）。
/// </summary>
public sealed class MockClippingMcpClient : IClippingMcpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task<byte[]> GenerateDraftAsync(string planJson, CancellationToken cancellationToken = default)
    {
        var segments = ReadSegments(planJson);
        var totalDuration = ReadTotalDuration(planJson);
        var draft = new
        {
            draft_roaming_id = Guid.NewGuid().ToString("N"),
            created_at = DateTime.UtcNow,
            mock = true,
            materials = new
            {
                videos = segments.Select(segment => new { segment.Index, segment.Source, segment.Duration })
            },
            summary = new { segment_count = segments.Count, total_duration = totalDuration }
        };
        return Task.FromResult(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft, JsonOptions)));
    }

    /// <summary>从方案 JSON 读取片段列表（index/source/duration）；解析失败返回空列表。</summary>
    private static IReadOnlyList<MockSegment> ReadSegments(string planJson)
    {
        try
        {
            using var document = JsonDocument.Parse(planJson);
            if (ReadValue(document.RootElement, "segments") is not { ValueKind: JsonValueKind.Array } segments) return [];
            var result = new List<MockSegment>();
            foreach (var element in segments.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                var index = ReadValue(element, "index") is { ValueKind: JsonValueKind.Number } indexElement && indexElement.TryGetInt32(out var parsedIndex) ? parsedIndex : 0;
                var source = ReadValue(element, "source") is { ValueKind: JsonValueKind.String } sourceElement ? sourceElement.GetString() : null;
                var duration = ReadValue(element, "duration") is { ValueKind: JsonValueKind.Number } durationElement && durationElement.TryGetInt32(out var parsedDuration) ? parsedDuration : 0;
                if (string.IsNullOrWhiteSpace(source)) continue;
                result.Add(new MockSegment(index, source, duration));
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>从方案 JSON 读取总时长；解析失败返回 0。</summary>
    private static int ReadTotalDuration(string planJson)
    {
        try
        {
            using var document = JsonDocument.Parse(planJson);
            return ReadValue(document.RootElement, "total_duration") is { ValueKind: JsonValueKind.Number } duration && duration.TryGetInt32(out var parsed) ? parsed : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    /// <summary>按蛇形键读取 JSON 属性值；兼容 System.Text.Json 驼峰序列化形态。</summary>
    private static JsonElement? ReadValue(JsonElement element, string snakeName)
    {
        if (element.TryGetProperty(snakeName, out var value)) return value;
        var parts = snakeName.Split('_');
        var camelName = parts.Length == 1 ? parts[0] : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return element.TryGetProperty(camelName, out value) ? value : null;
    }

    private sealed record MockSegment(int Index, string Source, int Duration);
}
