using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Family;

/// <summary>家庭成员实体，归属家庭（home），是家庭上下文的核心维度。</summary>
[Table("family_members")]
public sealed class FamilyMember
{
    /// <summary>成员主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>成员显示名。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>与户主关系，例如"self""spouse""child"等。</summary>
    [Column("relation")] public string Relation { get; set; } = null!;
    /// <summary>生日（UTC），可空表示未知。</summary>
    [Column("birthday")] public DateTime? Birthday { get; set; }
    /// <summary>是否标记为老人，影响健康建议与通知策略。</summary>
    [Column("is_elderly")] public bool IsElderly { get; set; }
    /// <summary>是否标记为儿童，影响自动化与权限。</summary>
    [Column("is_child")] public bool IsChild { get; set; }
    /// <summary>是否家庭主用户。</summary>
    [Column("is_primary")] public bool IsPrimary { get; set; }
    /// <summary>成员生命周期状态，参见 <see cref="FamilyMemberStatus"/>。</summary>
    [Column("member_status")] public string MemberStatus { get; set; } = FamilyMemberStatus.Active;
    /// <summary>成员偏好 JSON，由管家与建议系统解析。</summary>
    [Column("preferences_json")] public string? Preferences { get; set; }
    /// <summary>创建成员的用户标识。</summary>
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    /// <summary>终态更正操作者用户标识，仅在终态变更时填写。</summary>
    [Column("terminal_corrected_by_user_id")] public long? TerminalCorrectedByUserId { get; set; }
    /// <summary>终态更正原因，可审计。</summary>
    [Column("terminal_correction_reason")] public string? TerminalCorrectionReason { get; set; }
    /// <summary>终态更正时间（UTC）。</summary>
    [Column("terminal_corrected_at")] public DateTime? TerminalCorrectedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>家庭知识实体，按 key 写入并保留来源与解决结果。</summary>
[Table("family_knowledge")]
public sealed class FamilyKnowledge
{
    /// <summary>知识主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>知识分类，如"饮食偏好""作息"等。</summary>
    [Column("category")] public string Category { get; set; } = null!;
    /// <summary>知识键，同家庭内用于去重与冲突合并。</summary>
    [Column("knowledge_key")] public string Key { get; set; } = null!;
    /// <summary>知识值。</summary>
    [Column("knowledge_value")] public string Value { get; set; } = null!;
    /// <summary>补充说明。</summary>
    [Column("notes")] public string? Notes { get; set; }
    /// <summary>来源类型，参见 <see cref="FamilyKnowledgeSourceType"/>。</summary>
    [Column("source_type")] public string SourceType { get; set; } = FamilyKnowledgeSourceType.Member;
    /// <summary>来源成员主键，系统 AI 来源时为空。</summary>
    [Column("source_member_id")] public long? SourceMemberId { get; set; }
    /// <summary>置信度，范围 0-1。</summary>
    [Column("confidence_score")] public decimal ConfidenceScore { get; set; }
    /// <summary>冲突解决策略，参见 <see cref="FamilyKnowledgeConflictResolutionStrategy"/>。</summary>
    [Column("conflict_resolution_strategy")] public string ConflictResolutionStrategy { get; set; } = FamilyKnowledgeConflictResolutionStrategy.Latest;
    /// <summary>冲突解决结果摘要。</summary>
    [Column("resolution_summary")] public string? ResolutionSummary { get; set; }
    /// <summary>创建知识条目的用户标识，系统来源可为空。</summary>
    [Column("created_by_user_id")] public long? CreatedByUserId { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>家庭决策历史实体，保留可追溯的决策与理由。</summary>
[Table("decision_history")]
public sealed class DecisionHistory
{
    /// <summary>决策主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>决策场景，如"晚餐安排""出行计划"等。</summary>
    [Column("scenario")] public string Scenario { get; set; } = null!;
    /// <summary>所做决策内容。</summary>
    [Column("decision_made")] public string DecisionMade { get; set; } = null!;
    /// <summary>决策理由说明。</summary>
    [Column("rationale")] public string? Rationale { get; set; }
    /// <summary>备选方案 JSON 数组。</summary>
    [Column("alternatives_json")] public string? Alternatives { get; set; }
    /// <summary>决策者关联的家庭成员主键。</summary>
    [Column("made_by_member_id")] public long? MadeByMemberId { get; set; }
    /// <summary>决策者用户标识（系统决策可为空）。</summary>
    [Column("made_by_user_id")] public long? MadeByUserId { get; set; }
    /// <summary>决策时间（UTC）。</summary>
    [Column("decided_at")] public DateTime DecidedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>家庭成员生命周期状态集合。</summary>
public static class FamilyMemberStatus
{
    /// <summary>正常在册。</summary>
    public const string Active = "active";
    /// <summary>短期离开（例如出差、探亲）。</summary>
    public const string Away = "away";
    /// <summary>永久离开家庭，状态更正需审计。</summary>
    public const string PermanentlyLeft = "permanently_left";
    /// <summary>已故，状态更正需审计。</summary>
    public const string Deceased = "deceased";
}

/// <summary>家庭上下文审计动作集合，决定 <see cref="FamilyAuditLog.Action"/> 的合法取值。</summary>
public static class FamilyAuditActions
{
    /// <summary>成员普通状态变更（含 active 与 away 双向）。</summary>
    public const string MemberCorrection = "member_correction";
    /// <summary>成员终态更正或终态恢复。</summary>
    public const string MemberTerminalRestore = "member_terminal_restore";
    /// <summary>知识条目写入。</summary>
    public const string KnowledgeWrite = "knowledge_write";
    /// <summary>知识同 key 冲突被解决。</summary>
    public const string KnowledgeConflictResolved = "knowledge_conflict_resolved";
    /// <summary>决策历史记录。</summary>
    public const string DecisionRecord = "decision_record";
    /// <summary>确认项被成员确认；由确认中心服务写入。</summary>
    public const string ConfirmationConfirm = "confirmation_confirm";
    /// <summary>确认项被成员拒绝；由确认中心服务写入。</summary>
    public const string ConfirmationDeny = "confirmation_deny";
    /// <summary>L1 确认项批量确认成功；由确认中心服务写入。</summary>
    public const string ConfirmationBatch = "confirmation_batch";
    /// <summary>管家动态被撤销；由管家动态服务写入。</summary>
    public const string ActivityUndo = "activity_undo";
    /// <summary>个人偏好收藏被创建；由收藏服务写入。</summary>
    public const string FavoriteCreate = "favorite_create";
    /// <summary>个人偏好收藏被更新；由收藏服务写入。</summary>
    public const string FavoriteUpdate = "favorite_update";
    /// <summary>个人偏好收藏被删除；由收藏服务写入。</summary>
    public const string FavoriteDelete = "favorite_delete";
    /// <summary>个人偏好收藏由对话导入；由收藏服务写入。</summary>
    public const string FavoriteImport = "favorite_import";
    /// <summary>个人连接器授权会话发起；由连接器授权服务写入。</summary>
    public const string ConnectorAuthorizeStarted = "connector_authorize_started";
    /// <summary>个人连接器授权回调完成，凭据引用落库；由连接器授权服务写入。</summary>
    public const string ConnectorAuthorizeCompleted = "connector_authorize_completed";
    /// <summary>个人连接器授权被撤销，凭据可用性失效；由连接器授权服务写入。</summary>
    public const string ConnectorAuthorizeRevoked = "connector_authorize_revoked";
    /// <summary>家庭成员角色被 owner/admin 受控变更；由租户成员服务写入。</summary>
    public const string TenantMemberRoleChanged = "tenant_member_role_changed";
    /// <summary>家庭成员启用/停用状态被 owner/admin 受控变更；由租户成员服务写入。</summary>
    public const string TenantMemberStatusChanged = "tenant_member_status_changed";
    /// <summary>家庭成员邀请被创建；由邀请服务写入。</summary>
    public const string TenantInvitationCreated = "tenant_invitation_created";
    /// <summary>家庭成员邀请被撤销；由邀请服务写入。</summary>
    public const string TenantInvitationRevoked = "tenant_invitation_revoked";
    /// <summary>家庭成员邀请被已验证账户接受；由邀请服务写入。</summary>
    public const string TenantInvitationAccepted = "tenant_invitation_accepted";
    /// <summary>家庭 owner 角色被转让给同一家庭内 active 成员；由租户成员服务写入。</summary>
    public const string TenantOwnerTransferred = "tenant_owner_transferred";
    /// <summary>Web 导航偏好被 owner/admin 写入；由导航偏好服务写入。</summary>
    public const string WebNavigationPreferenceUpdated = "web_navigation_preference_updated";
    /// <summary>专家会话被创建；由会话服务写入。</summary>
    public const string ConversationCreate = "conversation_create";
    /// <summary>专家会话被重命名或重绑专家/连接器；由会话服务写入。</summary>
    public const string ConversationRename = "conversation_rename";
    /// <summary>专家会话被软删除；由会话服务写入。</summary>
    public const string ConversationDelete = "conversation_delete";
    /// <summary>Skill 运行被创建（SourceType=skill）；由 Skill 运行服务写入。</summary>
    public const string SkillRunCreated = "skill_run_created";
    /// <summary>Skill 运行动作被用户确认；由 Skill 运行服务写入（B25 消费）。</summary>
    public const string SkillActionConfirmed = "skill_action_confirmed";
    /// <summary>Skill 产物文件登记为生成文件；由 Skill 运行服务写入（B25 消费）。</summary>
    public const string SkillDraftRegistered = "skill_draft_registered";
    /// <summary>小红书笔记发布完成（L2 确认后执行）；由小红书发布服务写入（B27 消费）。</summary>
    public const string XhsNotePublished = "xhs_note_published";
}

/// <summary>家庭上下文审计目标类型集合，决定 <see cref="FamilyAuditLog.TargetType"/> 的合法取值。</summary>
public static class FamilyAuditTargetTypes
{
    /// <summary>目标为 <see cref="FamilyMember"/>。</summary>
    public const string FamilyMember = "family_member";
    /// <summary>目标为 <see cref="FamilyKnowledge"/>。</summary>
    public const string FamilyKnowledge = "family_knowledge";
    /// <summary>目标为 <see cref="DecisionHistory"/>。</summary>
    public const string DecisionHistory = "decision_history";
    /// <summary>目标为 <see cref="Entities.Steward.ConfirmationItem"/>。</summary>
    public const string ConfirmationItem = "confirmation_item";
    /// <summary>目标为 <see cref="Entities.Steward.StewardActivity"/>。</summary>
    public const string StewardActivity = "steward_activity";
    /// <summary>目标为 <see cref="Entities.Life.PersonalFavorite"/>。</summary>
    public const string PersonalFavorite = "personal_favorite";
    /// <summary>目标为 <see cref="Entities.SmartHome.ConnectorAuthorizationSession"/>。</summary>
    public const string ConnectorAuthorization = "connector_authorization";
    /// <summary>目标为 <see cref="HomeMind.Common.Model.Entities.TenantMember"/> 的角色/状态/owner 转让操作。</summary>
    public const string TenantMember = "tenant_member";
    /// <summary>目标为 <see cref="HomeMind.Common.Model.Entities.TenantMemberInvitation"/>。</summary>
    public const string TenantInvitation = "tenant_invitation";
    /// <summary>目标为 <see cref="HomeMind.Common.Model.Entities.WebNavigationPreference"/>。</summary>
    public const string WebNavigationPreference = "web_navigation_preference";
    /// <summary>目标为 <see cref="HomeMind.Common.Model.Entities.Conversation"/>（会话创建/重命名/软删除）。</summary>
    public const string Conversation = "conversation";
    /// <summary>目标为 SourceType=skill 的 <see cref="HomeMind.Common.Model.Entities.AgentRun"/>。</summary>
    public const string SkillRun = "skill_run";
    /// <summary>目标为 Skill 产物（剪映 .draft 草稿）文件登记。</summary>
    public const string SkillDraft = "skill_draft";
    /// <summary>目标为小红书（xhs）笔记发布（L2 确认后执行）。</summary>
    public const string XhsNote = "xhs_note";
}

/// <summary>家庭域审计记录实体；与管家动态、运行事件分离，专门承载 Family 域可审计动作。</summary>
[Table("family_audit_logs")]
public sealed class FamilyAuditLog
{
    /// <summary>审计主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>执行操作的用户标识，系统行为可为空。</summary>
    [Column("actor_user_id")] public long? ActorUserId { get; set; }
    /// <summary>审计动作，参见 <see cref="FamilyAuditActions"/>。</summary>
    [Column("action")] public string Action { get; set; } = null!;
    /// <summary>审计目标类型，参见 <see cref="FamilyAuditTargetTypes"/>。</summary>
    [Column("target_type")] public string TargetType { get; set; } = null!;
    /// <summary>审计目标主键，可空（例如新增主键尚未回填）。</summary>
    [Column("target_id")] public long? TargetId { get; set; }
    /// <summary>操作前状态 JSON 序列化结果。</summary>
    [Column("before_json")] public string? BeforeJson { get; set; }
    /// <summary>操作后状态 JSON 序列化结果。</summary>
    [Column("after_json")] public string? AfterJson { get; set; }
    /// <summary>操作原因，可空。</summary>
    [Column("reason")] public string? Reason { get; set; }
    /// <summary>关联的专家运行主键，可空；与运行/确认链路同源关联。</summary>
    [Column("related_run_id")] public long? RelatedRunId { get; set; }
    /// <summary>审计时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>家庭知识来源类型集合。</summary>
public static class FamilyKnowledgeSourceType
{
    /// <summary>由家庭成员主动写入。</summary>
    public const string Member = "member";
    /// <summary>由系统 AI 推断。</summary>
    public const string SystemAi = "system_ai";
}

/// <summary>家庭知识冲突解决策略集合。</summary>
public static class FamilyKnowledgeConflictResolutionStrategy
{
    /// <summary>以最近一条写入为准。</summary>
    public const string Latest = "latest";
    /// <summary>以权威来源（例如主用户）为准。</summary>
    public const string Authority = "authority";
    /// <summary>以多数来源的取值为准。</summary>
    public const string Majority = "majority";
}
