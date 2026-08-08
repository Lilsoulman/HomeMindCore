using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HomeMind.Common.Model.ViewModel.Data.Connectors;

/// <summary>小红书笔记搜索摘要视图；仅返回脱敏内容，不含登录态或凭据。</summary>
public sealed class XhsNoteSummaryView
{
    /// <summary>笔记标识。</summary>
    [Description("笔记标识。")]
    public string NoteId { get; init; } = "";

    /// <summary>笔记标题。</summary>
    [Description("笔记标题。")]
    public string Title { get; init; } = "";

    /// <summary>封面图片地址；无封面时为空。</summary>
    [Description("封面图片地址；无封面时为空。")]
    public string CoverUrl { get; init; } = "";

    /// <summary>作者昵称；不可得时为空。</summary>
    [Description("作者昵称；不可得时为空。")]
    public string AuthorName { get; init; } = "";

    /// <summary>笔记链接。</summary>
    [Description("笔记链接。")]
    public string Link { get; init; } = "";
}

/// <summary>小红书笔记详情视图；仅返回脱敏内容，不含登录态或凭据。</summary>
public sealed class XhsNoteDetailView
{
    /// <summary>笔记标识。</summary>
    [Description("笔记标识。")]
    public string NoteId { get; init; } = "";

    /// <summary>笔记标题。</summary>
    [Description("笔记标题。")]
    public string Title { get; init; } = "";

    /// <summary>笔记正文。</summary>
    [Description("笔记正文。")]
    public string Content { get; init; } = "";

    /// <summary>图片地址列表。</summary>
    [Description("图片地址列表。")]
    public IReadOnlyList<string> Images { get; init; } = [];

    /// <summary>笔记链接。</summary>
    [Description("笔记链接。")]
    public string Link { get; init; } = "";
}

/// <summary>小红书登录状态视图。</summary>
public sealed class XhsAuthStatusView
{
    /// <summary>是否已登录。</summary>
    [Description("是否已登录。")]
    public bool LoggedIn { get; init; }

    /// <summary>面向用户的登录状态描述。</summary>
    [Description("面向用户的登录状态描述。")]
    public string Message { get; init; } = "";
}

/// <summary>小红书扫码登录提示视图；仅创建授权会话时返回。</summary>
public sealed class XhsLoginHintView
{
    /// <summary>面向用户的扫码引导文案。</summary>
    [Description("面向用户的扫码引导文案。")]
    public string Hint { get; init; } = "";

    /// <summary>二维码内容或登录链接；无可展示内容时为空。</summary>
    [Description("二维码内容或登录链接；无可展示内容时为空。")]
    public string QrContent { get; init; } = "";
}

/// <summary>小红书图文/视频笔记发布请求体。</summary>
public sealed class XhsPublishRequest
{
    /// <summary>UUID 幂等键（可选）：同一键重复创建返回既有发布动作，不重复生成。</summary>
    [Description("UUID 幂等键（可选）：同一键重复创建返回既有发布动作，不重复生成。")]
    public string? IdempotencyKey { get; init; }

    /// <summary>发布类型：image（图文）或 video（视频）。</summary>
    [Required, Description("发布类型：image（图文）或 video（视频）。")]
    public string Type { get; init; } = "";

    /// <summary>笔记标题；图文不超过 20 个字符。</summary>
    [Required, StringLength(20), Description("笔记标题；图文不超过 20 个字符。")]
    public string Title { get; init; } = "";

    /// <summary>笔记正文；图文不超过 1000 个字符。</summary>
    [Required, StringLength(1000), Description("笔记正文；图文不超过 1000 个字符。")]
    public string Content { get; init; } = "";

    /// <summary>本地媒体路径列表：图文最多 18 张图片，视频恰 1 个视频文件。</summary>
    [Required, MinLength(1), Description("本地媒体路径列表：图文最多 18 张图片，视频恰 1 个视频文件。")]
    public IReadOnlyList<string> MediaPaths { get; init; } = [];

    /// <summary>话题标签列表；可空。</summary>
    [Description("话题标签列表；可空。")]
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>小红书笔记发布结果视图。</summary>
public sealed class XhsPublishResultView
{
    /// <summary>是否发布成功。</summary>
    [Description("是否发布成功。")]
    public bool Succeeded { get; init; }

    /// <summary>发布成功后的笔记标识；失败时为空。</summary>
    [Description("发布成功后的笔记标识；失败时为空。")]
    public string NoteId { get; init; } = "";

    /// <summary>面向用户的发布结果描述。</summary>
    [Description("面向用户的发布结果描述。")]
    public string Message { get; init; } = "";
}

/// <summary>确认小红书发布动作的请求体。</summary>
public sealed class XhsPublishConfirmRequest
{
    /// <summary>UUID 幂等键，同一键仅执行一次发布并重放首次结果。</summary>
    [Required, Description("UUID 幂等键，同一键仅执行一次发布并重放首次结果。")]
    public string IdempotencyKey { get; init; } = "";
}

/// <summary>小红书发布动作视图；用于确认中心的 L2 确认项展示。</summary>
public sealed class XhsPublishActionView
{
    /// <summary>动作主键。</summary>
    [Description("动作主键。")]
    public long ActionId { get; init; }

    /// <summary>动作类型（xhs_publish）。</summary>
    [Description("动作类型（xhs_publish）。")]
    public string ActionType { get; init; } = "";

    /// <summary>动作状态（pending / executing / executed / failed）。</summary>
    [Description("动作状态（pending / executing / executed / failed）。")]
    public string Status { get; init; } = "";

    /// <summary>动作标题。</summary>
    [Description("动作标题。")]
    public string Title { get; init; } = "";

    /// <summary>动作描述（内容摘要、配图数量等）。</summary>
    [Description("动作描述（内容摘要、配图数量等）。")]
    public string Description { get; init; } = "";

    /// <summary>风险等级（L2）。</summary>
    [Description("风险等级（L2）。")]
    public string RiskLevel { get; init; } = "L2";
}
