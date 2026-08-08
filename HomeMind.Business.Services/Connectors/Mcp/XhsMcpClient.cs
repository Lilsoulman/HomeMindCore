using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;

namespace HomeMind.Business.Services.Connectors.Mcp;

/// <summary>
/// 小红书（xhs）MCP 客户端实现：经 <see cref="IMcpProcessClient"/>（本地 stdio xhs-mcp 进程）
/// 调用小红书工具——xhs_auth_status / xhs_auth_login / xhs_search_note / xhs_get_note_detail /
/// xhs_publish_content。登录为扫码登录，凭据由 MCP 进程本机管理；工具参数与响应解析以部署的
/// xhs-mcp 版本契约为准，解析失败时只读操作降级为空结果、发布按 MCP 错误标记判定，不抛异常
/// （部署验证时校准字段映射）。本类不返回 cookie、登录态明文或 MCP 内部路径。
/// </summary>
public sealed class XhsMcpClient : IXhsMcpClient
{
    private readonly IMcpProcessClient _process;

    /// <summary>构造小红书 MCP 客户端。</summary>
    /// <param name="process">本地 stdio MCP 进程客户端（xhs-mcp）。</param>
    public XhsMcpClient(IMcpProcessClient process) => _process = process;

    /// <inheritdoc />
    public async Task<XhsAuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallSafeAsync("xhs_auth_status", null, cancellationToken);
        if (result is not JsonObject root) return new XhsAuthStatus(false, "小红书尚未登录。");
        var loggedIn = ReadBool(root, "loggedIn") ?? ReadBool(root, "logged_in") ?? ReadBool(root, "isLoggedIn")
            ?? (ReadString(root, "status") is { } status && status is "logged_in" or "authorized");
        if (loggedIn == true) return new XhsAuthStatus(true, "小红书已登录。");
        var hint = ReadString(root, "message") ?? ReadString(root, "hint");
        return new XhsAuthStatus(false, string.IsNullOrWhiteSpace(hint) ? "小红书尚未登录。" : hint);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await CallSafeAsync("xhs_auth_logout", null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<XhsLoginHint> TriggerLoginAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallSafeAsync("xhs_auth_login", null, cancellationToken);
        var text = result is JsonObject root ? ReadString(root, "message") ?? ReadString(root, "hint") : null;
        var qr = result is JsonObject qrRoot ? ReadString(qrRoot, "qrContent") ?? ReadString(qrRoot, "qr") ?? ReadString(qrRoot, "url") ?? "" : "";
        return new XhsLoginHint(string.IsNullOrWhiteSpace(text) ? "请在小红书 App 中扫描二维码完成登录。" : text, qr);
    }

    /// <inheritdoc />
    public async Task<XhsSearchResult> SearchNotesAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var arguments = new JsonObject { ["keyword"] = query };
        var result = await CallSafeAsync("xhs_search_note", arguments, cancellationToken);
        var notes = new List<XhsNoteSummary>();
        if (result is not JsonObject root) return new XhsSearchResult(notes);
        var array = ReadArray(root, "notes") ?? ReadArray(root, "items") ?? ReadArray(root, "data");
        if (array is null) return new XhsSearchResult(notes);
        foreach (var item in array.Take(limit))
        {
            if (item is not JsonObject note) continue;
            var id = ReadString(note, "noteId") ?? ReadString(note, "note_id") ?? ReadString(note, "id");
            var title = ReadString(note, "title") ?? "";
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(title)) continue;
            notes.Add(new XhsNoteSummary(
                id ?? title,
                title,
                ReadString(note, "cover") ?? ReadString(note, "coverUrl") ?? ReadString(note, "cover_url") ?? "",
                ReadString(note, "author") ?? ReadString(note, "authorName") ?? ReadString(note, "author_name") ?? "",
                ReadString(note, "link") ?? ReadString(note, "url") ?? ""));
        }
        return new XhsSearchResult(notes);
    }

    /// <inheritdoc />
    public async Task<XhsNoteDetail> GetNoteDetailAsync(string url, CancellationToken cancellationToken = default)
    {
        var arguments = new JsonObject { ["url"] = url };
        var result = await CallSafeAsync("xhs_get_note_detail", arguments, cancellationToken);
        if (result is not JsonObject root) return new XhsNoteDetail(url, "", "", [], url);
        var id = ReadString(root, "noteId") ?? ReadString(root, "note_id") ?? url;
        var title = ReadString(root, "title") ?? "";
        var content = ReadString(root, "content") ?? ReadString(root, "desc") ?? "";
        var images = new List<string>();
        if (ReadArray(root, "images") is { } imageArray)
        {
            foreach (var image in imageArray)
            {
                if (image is JsonObject imageObject)
                {
                    var urlValue = ReadString(imageObject, "url") ?? ReadString(imageObject, "infoList") ?? "";
                    if (string.IsNullOrWhiteSpace(urlValue)) continue;
                    images.Add(urlValue);
                }
                else if (image is JsonValue imageValue && imageValue.TryGetValue<string>(out var rawUrl) && !string.IsNullOrWhiteSpace(rawUrl))
                {
                    images.Add(rawUrl);
                }
            }
        }
        return new XhsNoteDetail(id, title, content, images, url);
    }

    /// <inheritdoc />
    public async Task<XhsPublishResult> PublishAsync(XhsPublishInput input, CancellationToken cancellationToken = default)
    {
        var media = new JsonArray();
        foreach (var path in input.MediaPaths) media.Add(path);
        var arguments = new JsonObject
        {
            ["type"] = input.Type,
            ["title"] = input.Title,
            ["content"] = input.Content,
            ["media_paths"] = media
        };
        if (input.Tags is { Count: > 0 })
        {
            var tags = new JsonArray();
            foreach (var tag in input.Tags) tags.Add(tag);
            arguments["tags"] = tags;
        }

        var result = await CallSafeAsync("xhs_publish_content", arguments, cancellationToken);
        if (result is not JsonObject root) return new XhsPublishResult(false, "", "小红书发布失败：MCP 无有效返回。");
        var noteId = ReadString(root, "noteId") ?? ReadString(root, "note_id") ?? ReadString(root, "id") ?? "";
        var message = ReadString(root, "message") ?? ReadString(root, "msg");
        if (ReadBool(root, "success") == false || ReadBool(root, "succeeded") == false)
            return new XhsPublishResult(false, "", string.IsNullOrWhiteSpace(message) ? "小红书发布失败。" : message);
        return new XhsPublishResult(true, noteId, string.IsNullOrWhiteSpace(message) ? "小红书笔记发布成功。" : message);
    }

    /// <summary>调用 MCP 工具并吞掉 <see cref="McpClientException"/>：只读操作降级为空结果、发布由解析层判为失败。</summary>
    private async Task<JsonNode?> CallSafeAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await _process.CallToolAsync(toolName, arguments, cancellationToken);
        }
        catch (McpClientException)
        {
            return null;
        }
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

    /// <summary>按常见键名读取布尔字段（蛇形/驼峰均可）；无匹配返回 null。</summary>
    private static bool? ReadBool(JsonObject root, string snakeName)
    {
        if (root.TryGetPropertyValue(snakeName, out var value) && value is JsonValue boolValue && boolValue.TryGetValue<bool>(out var flag)) return flag;
        var parts = snakeName.Split('_');
        if (parts.Length > 1)
        {
            var camelName = parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            if (root.TryGetPropertyValue(camelName, out value) && value is JsonValue camelValue && camelValue.TryGetValue<bool>(out var camelFlag)) return camelFlag;
        }
        return null;
    }

    /// <summary>按常见键名读取数组字段；无匹配返回 null。</summary>
    private static JsonArray? ReadArray(JsonObject root, string snakeName)
    {
        if (root.TryGetPropertyValue(snakeName, out var value) && value is JsonArray array) return array;
        var parts = snakeName.Split('_');
        if (parts.Length > 1)
        {
            var camelName = parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            if (root.TryGetPropertyValue(camelName, out value) && value is JsonArray camelArray) return camelArray;
        }
        return null;
    }
}
