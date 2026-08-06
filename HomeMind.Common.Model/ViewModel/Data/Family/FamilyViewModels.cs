namespace HomeMind.Common.Model.ViewModel.Data.Family;

/// <summary>家庭成员视图。</summary>
/// <param name="Id">成员主键。</param>
/// <param name="Name">成员显示名。</param>
/// <param name="Relation">与户主关系。</param>
/// <param name="Birthday">生日（UTC），可空。</param>
/// <param name="IsElderly">是否标记为老人。</param>
/// <param name="IsChild">是否标记为儿童。</param>
/// <param name="IsPrimary">是否家庭主用户。</param>
/// <param name="MemberStatus">成员生命周期状态。</param>
/// <param name="Preferences">成员偏好 JSON，可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record FamilyMemberView(long Id, string Name, string Relation, DateTime? Birthday, bool IsElderly, bool IsChild, bool IsPrimary, string MemberStatus, string? Preferences, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>家庭知识视图。</summary>
/// <param name="Id">知识主键。</param>
/// <param name="Category">知识分类。</param>
/// <param name="Key">知识键。</param>
/// <param name="Value">知识值。</param>
/// <param name="Notes">补充说明，可为空。</param>
/// <param name="SourceType">来源类型。</param>
/// <param name="SourceMemberId">来源成员主键，可为空。</param>
/// <param name="ConfidenceScore">置信度，0-1。</param>
/// <param name="ConflictResolutionStrategy">冲突解决策略。</param>
/// <param name="ResolutionSummary">冲突解决摘要，可为空。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record FamilyKnowledgeView(long Id, string Category, string Key, string Value, string? Notes, string SourceType, long? SourceMemberId, decimal ConfidenceScore, string ConflictResolutionStrategy, string? ResolutionSummary, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>家庭决策历史视图。</summary>
/// <param name="Id">决策主键。</param>
/// <param name="Scenario">决策场景。</param>
/// <param name="DecisionMade">决策内容。</param>
/// <param name="Rationale">决策理由，可为空。</param>
/// <param name="Alternatives">备选方案 JSON，可为空。</param>
/// <param name="MadeByMemberId">决策者成员主键，可为空。</param>
/// <param name="DecidedAt">决策时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record DecisionHistoryView(long Id, string Scenario, string DecisionMade, string? Rationale, string? Alternatives, long? MadeByMemberId, DateTime DecidedAt, DateTime UpdatedAt);

/// <summary>创建家庭成员的请求体。</summary>
/// <param name="Name">成员显示名，长度 1-128。</param>
/// <param name="Relation">与户主关系，长度 1-64。</param>
/// <param name="Birthday">生日（UTC），可空。</param>
/// <param name="IsElderly">是否标记为老人。</param>
/// <param name="IsChild">是否标记为儿童。</param>
/// <param name="IsPrimary">是否家庭主用户。</param>
/// <param name="MemberStatus">生命周期状态，可选"active"或"away"，其它由终态更正接口处理。</param>
/// <param name="Preferences">偏好 JSON，可为空。</param>
public sealed class FamilyMemberCreateRequest
{
    /// <summary>成员显示名，长度 1-128。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(128), System.ComponentModel.Description("成员显示名，长度 1-128。")]
    public string? Name { get; init; }
    /// <summary>与户主关系，长度 1-64。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(64), System.ComponentModel.Description("与户主关系，长度 1-64。")]
    public string? Relation { get; init; }
    /// <summary>生日（UTC），可空。</summary>
    [System.ComponentModel.Description("生日（UTC），可空。")]
    public DateTime? Birthday { get; init; }
    /// <summary>是否标记为老人。</summary>
    [System.ComponentModel.Description("是否标记为老人。")]
    public bool IsElderly { get; init; }
    /// <summary>是否标记为儿童。</summary>
    [System.ComponentModel.Description("是否标记为儿童。")]
    public bool IsChild { get; init; }
    /// <summary>是否家庭主用户。</summary>
    [System.ComponentModel.Description("是否家庭主用户。")]
    public bool IsPrimary { get; init; }
    /// <summary>生命周期状态，可选"active"或"away"。</summary>
    [System.ComponentModel.Description("生命周期状态，可选 active 或 away；终态由更正接口处理。")]
    public string? MemberStatus { get; init; }
    /// <summary>偏好 JSON，可为空。</summary>
    [System.ComponentModel.Description("偏好 JSON，可为空。")]
    public string? Preferences { get; init; }
}

/// <summary>部分更新家庭成员的请求体；仅允许在 active 与 away 之间切换。</summary>
public sealed class FamilyMemberUpdateRequest
{
    /// <summary>成员显示名，长度 1-128。</summary>
    [System.ComponentModel.DataAnnotations.StringLength(128), System.ComponentModel.Description("成员显示名，长度 1-128。")]
    public string? Name { get; init; }
    /// <summary>与户主关系，长度 1-64。</summary>
    [System.ComponentModel.DataAnnotations.StringLength(64), System.ComponentModel.Description("与户主关系，长度 1-64。")]
    public string? Relation { get; init; }
    /// <summary>生日（UTC），可空。</summary>
    [System.ComponentModel.Description("生日（UTC），可空。")]
    public DateTime? Birthday { get; init; }
    /// <summary>是否标记为老人。</summary>
    [System.ComponentModel.Description("是否标记为老人。")]
    public bool? IsElderly { get; init; }
    /// <summary>是否标记为儿童。</summary>
    [System.ComponentModel.Description("是否标记为儿童。")]
    public bool? IsChild { get; init; }
    /// <summary>是否家庭主用户。</summary>
    [System.ComponentModel.Description("是否家庭主用户。")]
    public bool? IsPrimary { get; init; }
    /// <summary>目标生命周期状态，必须为"active"或"away"。</summary>
    [System.ComponentModel.Description("目标状态，必须为 active 或 away；终态请走更正接口。")]
    public string? MemberStatus { get; init; }
    /// <summary>偏好 JSON，可为空。</summary>
    [System.ComponentModel.Description("偏好 JSON，可为空。")]
    public string? Preferences { get; init; }
}

/// <summary>终态更正或恢复的请求体；写入审计。</summary>
/// <param name="MemberStatus">目标状态；"permanently_left"、"deceased"或"active"或"away"。</param>
/// <param name="Reason">操作原因，长度 1-512；终态变更必填。</param>
public sealed record FamilyMemberCorrectionRequest(string MemberStatus, string? Reason);

/// <summary>家庭知识写入请求体。</summary>
public sealed class FamilyKnowledgeWriteRequest
{
    /// <summary>知识分类，固定 6 档：property/wifi/repair/cleaning/insurance/other。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.Description("知识分类，固定 6 档：property、wifi、repair、cleaning、insurance、other。")]
    public string? Category { get; init; }
    /// <summary>知识键，同家庭内用于去重与冲突合并，长度 1-256。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(256), System.ComponentModel.Description("知识键，长度 1-256。")]
    public string? Key { get; init; }
    /// <summary>知识值。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.Description("知识值。")]
    public string? Value { get; init; }
    /// <summary>补充说明，可为空。</summary>
    [System.ComponentModel.Description("补充说明，可为空。")]
    public string? Notes { get; init; }
    /// <summary>来源类型，可选"member"或"system_ai"；默认为"member"。</summary>
    [System.ComponentModel.Description("来源类型，可选 member 或 system_ai；默认为 member。")]
    public string? SourceType { get; init; }
    /// <summary>来源成员主键，source_type=member 时必填。</summary>
    [System.ComponentModel.Description("来源成员主键；source_type=member 时必填。")]
    public long? SourceMemberId { get; init; }
    /// <summary>置信度，0-1。</summary>
    [System.ComponentModel.DataAnnotations.Range(0.0, 1.0), System.ComponentModel.Description("置信度，0-1。")]
    public decimal ConfidenceScore { get; init; }
    /// <summary>冲突解决策略，可选"latest"/"authority"/"majority"；默认为"latest"。</summary>
    [System.ComponentModel.Description("冲突解决策略，可选 latest、authority、majority；默认为 latest。")]
    public string? ConflictResolutionStrategy { get; init; }
}

/// <summary>家庭知识冲突解决结果摘要。</summary>
/// <param name="KnowledgeId">本次写入的知识主键。</param>
/// <param name="ConflictKey">触发冲突的知识键。</param>
/// <param name="Strategy">采用的策略。</param>
/// <param name="ResolutionSummary">解决结果摘要。</param>
/// <param name="ConflictingIds">参与冲突解决的其它知识主键列表。</param>
public sealed record FamilyKnowledgeResolutionView(long KnowledgeId, string ConflictKey, string Strategy, string ResolutionSummary, IReadOnlyList<long> ConflictingIds);

/// <summary>家庭决策写入请求体。</summary>
public sealed class FamilyDecisionWriteRequest
{
    /// <summary>决策场景，长度 1-128。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(128), System.ComponentModel.Description("决策场景，长度 1-128。")]
    public string? Scenario { get; init; }
    /// <summary>所做决策内容。</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.Description("所做决策内容。")]
    public string? DecisionMade { get; init; }
    /// <summary>决策理由，可为空。</summary>
    [System.ComponentModel.Description("决策理由，可为空。")]
    public string? Rationale { get; init; }
    /// <summary>备选方案 JSON 数组，可为空。</summary>
    [System.ComponentModel.Description("备选方案 JSON 数组，可为空。")]
    public string? Alternatives { get; init; }
    /// <summary>决策者关联的家庭成员主键，可为空。</summary>
    [System.ComponentModel.Description("决策者成员主键，可为空。")]
    public long? MadeByMemberId { get; init; }
    /// <summary>决策时间（UTC），可空；为空时使用服务端当前 UTC。</summary>
    [System.ComponentModel.Description("决策时间（UTC），可空；为空时使用服务端当前 UTC。")]
    public DateTime? DecidedAt { get; init; }
}

/// <summary>家庭审计日志视图；用于合规排障，不返回凭据。</summary>
public sealed record FamilyAuditLogView(long Id, string Action, string TargetType, long? TargetId, string? BeforeJson, string? AfterJson, string? Reason, long? RelatedRunId, DateTime CreatedAt);
