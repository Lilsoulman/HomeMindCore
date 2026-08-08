namespace HomeMind.Business.IServices.Connector;

/// <summary>小红书登录状态摘要。</summary>
/// <param name="LoggedIn">是否已登录。</param>
/// <param name="Message">面向用户的登录状态描述。</param>
public sealed record XhsAuthStatus(bool LoggedIn, string Message);

/// <summary>扫码登录提示信息；二维码展示方式随 MCP 实现而定（终端二维码/图片/链接）。</summary>
/// <param name="Hint">面向用户的扫码引导文案。</param>
/// <param name="QrContent">二维码内容或登录链接；无可展示内容时为空。</param>
public sealed record XhsLoginHint(string Hint, string QrContent = "");

/// <summary>小红书笔记搜索摘要列表。</summary>
/// <param name="Notes">笔记摘要列表。</param>
public sealed record XhsSearchResult(IReadOnlyList<XhsNoteSummary> Notes);

/// <summary>小红书笔记摘要。</summary>
/// <param name="NoteId">笔记标识。</param>
/// <param name="Title">笔记标题。</param>
/// <param name="CoverUrl">封面图片地址；无封面时为空。</param>
/// <param name="AuthorName">作者昵称；不可得时为空。</param>
/// <param name="Link">笔记链接。</param>
public sealed record XhsNoteSummary(string NoteId, string Title, string CoverUrl = "", string AuthorName = "", string Link = "");

/// <summary>小红书笔记详情（只读视图，不含评论互动等扩展字段）。</summary>
/// <param name="NoteId">笔记标识。</param>
/// <param name="Title">笔记标题。</param>
/// <param name="Content">笔记正文。</param>
/// <param name="Images">图片地址列表。</param>
/// <param name="Link">笔记链接。</param>
public sealed record XhsNoteDetail(string NoteId, string Title, string Content, IReadOnlyList<string> Images, string Link);

/// <summary>小红书图文/视频笔记发布参数。</summary>
/// <param name="Type">发布类型：image（图文，标题≤20 字符、正文≤1000 字、图片≤18）或 video（视频，恰 1 个文件）。</param>
/// <param name="Title">笔记标题。</param>
/// <param name="Content">笔记正文。</param>
/// <param name="MediaPaths">本地媒体路径列表（图片多张或视频单条）。</param>
/// <param name="Tags">话题标签列表；可空。</param>
public sealed record XhsPublishInput(string Type, string Title, string Content, IReadOnlyList<string> MediaPaths, IReadOnlyList<string>? Tags);

/// <summary>小红书发布结果。</summary>
/// <param name="Succeeded">是否发布成功。</param>
/// <param name="NoteId">发布成功后的笔记标识；失败时为空。</param>
/// <param name="Message">面向用户的发布结果描述。</param>
public sealed record XhsPublishResult(bool Succeeded, string NoteId, string Message);
