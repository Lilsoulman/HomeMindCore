namespace HomeMind.Common.Model.ViewModel.Data.Steward;

/// <summary>单项确认确认项的请求体；幂等键仅校验格式，去重由确认项状态流转保证。</summary>
public sealed class ConfirmationConfirmRequest
{
    /// <summary>幂等键，UUID 格式；重复提交不会产生重复副作用。</summary>
    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.Description("幂等键，UUID 格式；重复提交不会产生重复副作用。")]
    public string? IdempotencyKey { get; init; }
}

/// <summary>拒绝确认项的请求体；原因写入审计与管家动态。</summary>
public sealed class ConfirmationDenyRequest
{
    /// <summary>拒绝原因，长度 1-512，用于审计与管家动态展示。</summary>
    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.DataAnnotations.StringLength(512),
     System.ComponentModel.Description("拒绝原因，长度 1-512，用于审计与管家动态展示。")]
    public string? Reason { get; init; }
}

/// <summary>L1 批量确认请求体；任一违规项整体拒绝，不做部分成功。</summary>
public sealed class ConfirmationBatchConfirmRequest
{
    /// <summary>待确认的 L1 确认项 ID 列表，1-50 个。</summary>
    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.DataAnnotations.MinLength(1),
     System.ComponentModel.DataAnnotations.MaxLength(50),
     System.ComponentModel.Description("待确认的 L1 确认项 ID 列表，1-50 个；重复 ID、L2/L3、跨家庭、已终态或过期项会导致整个请求被拒绝。")]
    public long[]? ConfirmationIds { get; init; }
    /// <summary>幂等键，UUID 格式；同一键仅返回首次记录的结果。</summary>
    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.Description("幂等键，UUID 格式；同一键仅返回首次记录的结果。")]
    public string? IdempotencyKey { get; init; }
}

/// <summary>L1 批量确认结果视图；只含家庭安全的确认项摘要。</summary>
/// <param name="ConfirmedCount">本次确认的确认项数量。</param>
/// <param name="Items">确认后的确认项视图列表。</param>
public sealed record ConfirmationBatchResultView(int ConfirmedCount, IReadOnlyList<ConfirmationItemView> Items);

/// <summary>管家活动视图。</summary>
/// <param name="Id">活动主键。</param>
/// <param name="RunId">关联的 AgentRun 主键，可为空。</param>
/// <param name="Category">活动分类。</param>
/// <param name="Title">活动标题。</param>
/// <param name="Description">活动描述，可为空。</param>
/// <param name="RiskLevel">风险等级。</param>
/// <param name="Status">活动状态。</param>
/// <param name="ResultSummary">结果摘要，可为空。</param>
/// <param name="Undoable">是否可被撤销。</param>
/// <param name="UndoneAt">撤销时间（UTC），可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record StewardActivityView(long Id, long? RunId, string Category, string Title, string? Description, string RiskLevel, string Status, string? ResultSummary, bool Undoable, DateTime? UndoneAt, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>确认项视图。</summary>
/// <param name="Id">确认项主键。</param>
/// <param name="ActivityId">关联活动主键，可为空。</param>
/// <param name="RiskLevel">风险等级。</param>
/// <param name="Title">确认项标题。</param>
/// <param name="Description">描述，可为空。</param>
/// <param name="ImpactSummary">影响摘要，可为空。</param>
/// <param name="SuggestedAction">建议动作文案，可为空。</param>
/// <param name="Status">确认项状态。</param>
/// <param name="ExpiresAt">到期时间（UTC）。</param>
/// <param name="ConfirmedAt">确认时间（UTC）。</param>
/// <param name="DeniedAt">拒绝时间（UTC）。</param>
/// <param name="ExpiredAt">过期时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record ConfirmationItemView(long Id, long? ActivityId, string RiskLevel, string Title, string? Description, string? ImpactSummary, string? SuggestedAction, string Status, DateTime? ExpiresAt, DateTime? ConfirmedAt, DateTime? DeniedAt, DateTime? ExpiredAt, DateTime UpdatedAt);
