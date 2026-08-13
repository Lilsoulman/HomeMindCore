using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<XhsMcpClient> _logger;

    /// <summary>构造小红书 MCP 客户端。</summary>
    /// <param name="process">本地 stdio MCP 进程客户端（xhs-mcp）。</param>
    public XhsMcpClient(IMcpProcessClient process, ILogger<XhsMcpClient> logger)
    {
        _process = process;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<XhsAuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default)
        => await GetAuthStatusAsync(null, cancellationToken);

    public async Task<XhsAuthStatus> GetAuthStatusAsync(string? credentialRef, CancellationToken cancellationToken = default)
    {
        var result = await CallRequiredAsync("xhs_auth_status", WithCredential(credentialRef), cancellationToken);
        if (result is not JsonObject root) return new XhsAuthStatus(false, "小红书尚未登录。");
        var loggedIn = ReadBool(root, "loggedIn") ?? ReadBool(root, "logged_in") ?? ReadBool(root, "isLoggedIn")
            ?? (ReadString(root, "status") is { } status && status is "logged_in" or "authorized");
        if (loggedIn == true) return new XhsAuthStatus(true, "小红书已登录。");
        var hint = ReadString(root, "message") ?? ReadString(root, "hint");
        return new XhsAuthStatus(false, string.IsNullOrWhiteSpace(hint) ? "小红书尚未登录。" : hint);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
        => await LogoutAsync(null, cancellationToken);

    public async Task LogoutAsync(string? credentialRef, CancellationToken cancellationToken = default)
    {
        await CallOptionalAsync("xhs_auth_logout", WithCredential(credentialRef), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<XhsLoginHint> TriggerLoginAsync(CancellationToken cancellationToken = default)
        => await TriggerLoginAsync(null, cancellationToken);

    public async Task<XhsLoginHint> TriggerLoginAsync(string? credentialRef, CancellationToken cancellationToken = default)
    {
        var result = await CallRequiredAsync("xhs_auth_login", WithCredential(credentialRef), cancellationToken);
        var text = result is JsonObject root ? ReadString(root, "message") ?? ReadString(root, "hint") : null;
        var qr = result is JsonObject qrRoot ? ReadString(qrRoot, "qrContent") ?? ReadString(qrRoot, "qr") ?? ReadString(qrRoot, "url") ?? "" : "";
        return new XhsLoginHint(string.IsNullOrWhiteSpace(text) ? "请在小红书 App 中扫描二维码完成登录。" : text, qr);
    }

    /// <inheritdoc />
    public async Task<XhsSearchResult> SearchNotesAsync(string query, int limit, CancellationToken cancellationToken = default)
        => await SearchNotesAsync(query, limit, null, cancellationToken);

    public async Task<XhsSearchResult> SearchNotesAsync(string query, int limit, string? credentialRef, CancellationToken cancellationToken = default)
    {
        var arguments = WithCredential(credentialRef);
        arguments["keyword"] = query;
        var result = await CallRequiredAsync("xhs_search_note", arguments, cancellationToken);
        var notes = new List<XhsNoteSummary>();
        if (result is not JsonObject root)
            throw UnexpectedResponse("xhs_search_note", result, "搜索响应不是对象。");
        var array = ReadSearchArray(root);
        if (array is null)
            throw UnexpectedResponse("xhs_search_note", result, "搜索响应未包含笔记数组。");
        foreach (var item in array.Take(limit))
        {
            if (item is not JsonObject note) continue;
            var card = ReadObject(note, "noteCard");
            var id = ReadString(note, "noteId") ?? ReadString(note, "note_id") ?? ReadString(note, "id")
                ?? (card is null ? null : ReadString(card, "noteId") ?? ReadString(card, "note_id") ?? ReadString(card, "id"));
            var xsecToken = ReadString(note, "xsecToken") ?? ReadString(note, "xsec_token")
                ?? (card is null ? null : ReadString(card, "xsecToken") ?? ReadString(card, "xsec_token"));
            var title = card is null
                ? ReadString(note, "title") ?? ""
                : ReadString(card, "displayTitle") ?? ReadString(card, "title") ?? ReadString(note, "title") ?? "";
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(title)) continue;
            var cover = card is not null && ReadObject(card, "cover") is { } coverObj
                ? ReadString(coverObj, "urlDefault") ?? ReadString(coverObj, "url") ?? ""
                : "";
            var author = card is not null && ReadObject(card, "user") is { } userObj
                ? ReadString(userObj, "nickName") ?? ReadString(userObj, "nickname") ?? ""
                : "";
            var link = ReadString(note, "link") ?? ReadString(note, "url")
                ?? (card is null ? null : ReadString(card, "link") ?? ReadString(card, "url"));
            if (string.IsNullOrWhiteSpace(link) && !string.IsNullOrWhiteSpace(id))
                link = BuildNoteLink(id, xsecToken);
            else if (!string.IsNullOrWhiteSpace(link) && !string.IsNullOrWhiteSpace(xsecToken))
                link = AddXsecToken(link, xsecToken);

            notes.Add(new XhsNoteSummary(
                id ?? title,
                title,
                cover is { Length: > 0 } ? cover : ReadString(note, "cover") ?? ReadString(note, "coverUrl") ?? ReadString(note, "cover_url") ?? "",
                author is { Length: > 0 } ? author : ReadString(note, "author") ?? ReadString(note, "authorName") ?? ReadString(note, "author_name") ?? "",
                link ?? ""));
        }
        if (array.Count > 0 && notes.Count == 0)
            throw UnexpectedResponse("xhs_search_note", result, "笔记数组中没有可识别的笔记项。");
        return new XhsSearchResult(notes);
    }

    /// <inheritdoc />
    public async Task<XhsNoteDetail> GetNoteDetailAsync(string url, CancellationToken cancellationToken = default)
        => await GetNoteDetailAsync(url, null, cancellationToken);

    public async Task<XhsNoteDetail> GetNoteDetailAsync(string url, string? credentialRef, CancellationToken cancellationToken = default)
    {
        var arguments = WithCredential(credentialRef);
        arguments["url"] = url;
        var result = await CallRequiredAsync("xhs_get_note_detail", arguments, cancellationToken);
        if (result is not JsonObject root)
            throw UnexpectedResponse("xhs_get_note_detail", result, "详情响应不是对象。");
        ThrowForNoteDetailFailure(root);
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
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content) && id == url)
            throw UnexpectedResponse("xhs_get_note_detail", result, "详情响应缺少可识别的笔记字段。");
        return new XhsNoteDetail(id, title, content, images, url);
    }

    private static void ThrowForNoteDetailFailure(JsonObject root)
    {
        if (ReadBool(root, "success") != false) return;

        var error = ReadString(root, "error") ?? "";
        var message = ReadString(root, "message");
        if (error is "InvalidNoteUrl" or "MissingXsecToken")
            throw new XhsNoteDetailException(422, string.IsNullOrWhiteSpace(message) ? "笔记链接无效。" : message);
        if (error is "FeedError" or "NotFound")
            throw new XhsNoteDetailException(404, "笔记不存在或当前账号无权访问。");
    }

    /// <inheritdoc />
    public async Task<XhsPublishResult> PublishAsync(XhsPublishInput input, CancellationToken cancellationToken = default)
        => await PublishAsync(input, null, cancellationToken);

    public async Task<XhsPublishResult> PublishAsync(XhsPublishInput input, string? credentialRef, CancellationToken cancellationToken = default)
    {
        var media = new JsonArray();
        foreach (var path in input.MediaPaths) media.Add(path);
        var arguments = WithCredential(credentialRef);
        arguments["type"] = input.Type;
        arguments["title"] = input.Title;
        arguments["content"] = input.Content;
        arguments["media_paths"] = media;
        if (input.Tags is { Count: > 0 })
        {
            var tags = new JsonArray();
            foreach (var tag in input.Tags) tags.Add(tag);
            arguments["tags"] = tags;
        }

        var result = await CallOptionalAsync("xhs_publish_content", arguments, cancellationToken);
        if (result is not JsonObject root) return new XhsPublishResult(false, "", "小红书发布失败：MCP 无有效返回。");
        var noteId = ReadString(root, "noteId") ?? ReadString(root, "note_id") ?? ReadString(root, "id") ?? "";
        var message = ReadString(root, "message") ?? ReadString(root, "msg");
        if (ReadBool(root, "success") == false || ReadBool(root, "succeeded") == false)
            return new XhsPublishResult(false, "", string.IsNullOrWhiteSpace(message) ? "小红书发布失败。" : message);
        return new XhsPublishResult(true, noteId, string.IsNullOrWhiteSpace(message) ? "小红书笔记发布成功。" : message);
    }

    private static JsonObject WithCredential(string? credentialRef) =>
        string.IsNullOrWhiteSpace(credentialRef) ? new JsonObject() : new JsonObject { ["credentialRef"] = credentialRef };

    private async Task<JsonNode?> CallRequiredAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await _process.CallToolAsync(toolName, arguments, cancellationToken);
        }
        catch (McpClientException error)
        {
            LogMcpFailure(toolName, error);
            throw;
        }
    }

    private async Task<JsonNode?> CallOptionalAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await _process.CallToolAsync(toolName, arguments, cancellationToken);
        }
        catch (McpClientException error)
        {
            LogMcpFailure(toolName, error);
            return null;
        }
    }

    private McpClientException UnexpectedResponse(string toolName, JsonNode? response, string reason)
    {
        _logger.LogWarning("XHS MCP response structure is incompatible. Tool={ToolName} Reason={Reason} ResponseStructure={ResponseStructure}",
            toolName, reason, DescribeStructure(response));
        return new McpClientException("小红书 MCP 返回结构不兼容。");
    }

    private void LogMcpFailure(string toolName, McpClientException error) =>
        _logger.LogWarning("XHS MCP call failed. Tool={ToolName} FailureCategory={FailureCategory} ErrorSummary={ErrorSummary}",
            toolName, ClassifyFailure(error.Message), SafeErrorSummary(error.Message));

    private static string ClassifyFailure(string message)
    {
        if (ContainsAny(message, "429", "rate limit", "too many", "限流", "频繁")) return "rate_limited";
        if (ContainsAny(message, "login", "logged", "auth", "cookie", "登录", "会话", "未授权")) return "authentication";
        return "upstream";
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static string SafeErrorSummary(string message) => ClassifyFailure(message) switch
    {
        "rate_limited" => "上游服务拒绝了当前请求。",
        "authentication" => "上游登录状态或会话不可用。",
        _ => "上游服务调用失败。"
    };

    private static string DescribeStructure(JsonNode? node) => node switch
    {
        null => "null",
        JsonValue => "value",
        JsonArray array => $"array[{array.Count}]",
        JsonObject obj => "object{" + string.Join(',', obj.Select(pair => IsSensitiveField(pair.Key)
            ? "[REDACTED]:" + DescribeStructure(pair.Value)
            : pair.Key + ":" + DescribeStructure(pair.Value))) + "}",
        _ => "unknown"
    };

    private static bool IsSensitiveField(string name) =>
        ContainsAny(name, "cookie", "token", "authorization", "credential", "password", "secret");

    private static string BuildNoteLink(string noteId, string? xsecToken)
    {
        var link = "https://www.xiaohongshu.com/explore/" + Uri.EscapeDataString(noteId);
        return string.IsNullOrWhiteSpace(xsecToken) ? link : AddXsecToken(link, xsecToken);
    }

    private static string AddXsecToken(string link, string xsecToken)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            !uri.Host.EndsWith("xiaohongshu.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Contains("xsec_token=", StringComparison.OrdinalIgnoreCase))
            return link;

        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return link + separator + "xsec_token=" + Uri.EscapeDataString(xsecToken) + "&xsec_source=pc_feed";
    }

    private static JsonArray? ReadSearchArray(JsonObject root)
    {
        var direct = ReadArray(root, "notes") ?? ReadArray(root, "items") ?? ReadArray(root, "feeds") ?? ReadArray(root, "data");
        if (direct is not null) return direct;
        var data = ReadObject(root, "data");
        return data is null ? null : ReadArray(data, "notes") ?? ReadArray(data, "items") ?? ReadArray(data, "feeds");
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

    /// <summary>按常见键名读取对象字段；无匹配返回 null。</summary>
    private static JsonObject? ReadObject(JsonObject root, string snakeName)
    {
        if (root.TryGetPropertyValue(snakeName, out var value) && value is JsonObject obj) return obj;
        var parts = snakeName.Split('_');
        if (parts.Length > 1)
        {
            var camelName = parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            if (root.TryGetPropertyValue(camelName, out value) && value is JsonObject camelObj) return camelObj;
        }
        return null;
    }
}
