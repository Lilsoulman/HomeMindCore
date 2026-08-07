using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HomeMind.Common.Model.ViewModel.Data.Identity;

/// <summary>家庭成员摘要视图，包含账户资料、角色、状态与乐观锁版本。</summary>
public sealed record TenantMemberSummaryView(
    long UserId,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string Status,
    DateTime JoinedAt,
    string Timezone,
    string Locale,
    bool IsCurrentUserOwner,
    bool HasPendingInvitation,
    long RowVersion);

/// <summary>家庭成员角色变更请求；新角色不得为 <c>owner</c>。</summary>
public sealed class TenantMemberRoleUpdateRequest
{
    /// <summary>目标角色；<c>admin</c>/<c>member</c>/<c>viewer</c>，<c>owner</c> 必须走 owner-transfer。</summary>
    [Required, StringLength(16), Description("目标角色：admin/member/viewer；owner 必须走 owner-transfer。")]
    public string NewRole { get; init; } = null!;

    /// <summary>乐观锁版本号；与服务端不一致返回 409。</summary>
    [Required, Description("乐观锁版本号，与服务端不一致返回 409/40901。")]
    public long RowVersion { get; init; }
}

/// <summary>家庭成员启用/停用状态变更请求。</summary>
public sealed class TenantMemberStatusUpdateRequest
{
    /// <summary>目标状态：<c>active</c> / <c>suspended</c>。</summary>
    [Required, StringLength(16), Description("目标状态：active/suspended。")]
    public string NewStatus { get; init; } = null!;

    /// <summary>变更原因；停用时必填，会写入审计与日志。</summary>
    [StringLength(512), Description("变更原因；停用时必填。")]
    public string? Reason { get; init; }

    /// <summary>乐观锁版本号；与服务端不一致返回 409。</summary>
    [Required, Description("乐观锁版本号。")]
    public long RowVersion { get; init; }
}

/// <summary>家庭 owner 转让请求；发起人必须为当前 active owner。</summary>
public sealed class TenantOwnerTransferRequest
{
    /// <summary>新 owner 用户主键；必须为当前家庭 active 成员。</summary>
    [Required, Description("新 owner 用户主键；必须为当前家庭 active 成员。")]
    public long NewOwnerUserId { get; init; }

    /// <summary>乐观锁版本号；防止并发转让。</summary>
    [Required, Description("乐观锁版本号；防止并发转让。")]
    public long RowVersion { get; init; }
}

/// <summary>家庭成员邀请视图；subject_hash 永远以十六进制摘要形式返回。</summary>
public sealed record TenantMemberInvitationView(
    long Id,
    long InvitedByUserId,
    string SubjectKind,
    string SubjectHashHex,
    string ProposedRole,
    string Status,
    DateTime ExpiresAt,
    long? AcceptedUserId,
    DateTime? AcceptedAt,
    DateTime? RevokedAt,
    DateTime CreatedAt,
    long RowVersion);

/// <summary>家庭成员邀请列表分页视图；按过期时间升序，cursor 由上次响应返回。</summary>
public sealed record TenantMemberInvitationListView(
    IReadOnlyList<TenantMemberInvitationView> Items,
    string? Cursor);

/// <summary>家庭成员邀请创建请求；服务端按 phone 哈希匹配 user_identities。</summary>
public sealed class TenantMemberInvitationCreateRequest
{
    /// <summary>受邀人手机号；服务端规范化为 E.164 后计算 SHA-256。</summary>
    [Required, StringLength(32), Description("受邀人手机号；服务端规范化为 E.164 后计算 SHA-256。")]
    public string Phone { get; init; } = null!;

    /// <summary>接受后授予的角色：<c>admin</c>/<c>member</c>/<c>viewer</c>，不得为 <c>owner</c>。</summary>
    [Required, StringLength(16), Description("接受后授予的角色：admin/member/viewer；不得为 owner。")]
    public string ProposedRole { get; init; } = null!;
}

/// <summary>家庭成员邀请接受请求；当前用户调用，携带待验证手机号。</summary>
public sealed class TenantMemberInvitationAcceptRequest
{
    /// <summary>邀请主键；服务端按 (tenant_id, initiator_user_id) 校验归属。</summary>
    [Required, Description("邀请主键。")]
    public long InvitationId { get; init; }

    /// <summary>当前用户的手机号原文；服务端重新计算 SHA-256 与邀请记录比对。</summary>
    [Required, StringLength(32), Description("当前用户的手机号原文；服务端重新计算 SHA-256 与邀请记录比对。")]
    public string Phone { get; init; } = null!;
}

/// <summary>Web 导航单条 route 视图，包含命中默认值与否。</summary>
public sealed record WebNavigationRouteView(
    string RouteKey,
    bool Enabled,
    int SortOrder,
    bool IsCustomized);

/// <summary>Web 导航偏好视图：当前家庭当前角色可见的所有 route_key。</summary>
public sealed record WebNavigationPreferencesView(
    string Role,
    IReadOnlyList<WebNavigationRouteView> Routes,
    DateTime? UpdatedAt);

/// <summary>Web 导航单条偏好更新项；route_key 须命中 <c>NexusWebNavigationKeys.All</c>。</summary>
public sealed class WebNavigationPreferenceUpdateItem
{
    /// <summary>已发布的 route_key；服务端按白名单校验。</summary>
    [Required, StringLength(64), Description("已发布的 route_key；服务端按白名单校验。")]
    public string RouteKey { get; init; } = null!;

    /// <summary>是否在菜单中显示。</summary>
    [Required, Description("是否在菜单中显示。")]
    public bool Enabled { get; init; }

    /// <summary>显示顺序；值越小越靠前，范围 0-1000。</summary>
    [Required, Range(0, 1000), Description("显示顺序；值越小越靠前，范围 0-1000。")]
    public int SortOrder { get; init; }
}

/// <summary>Web 导航偏好更新请求；仅 owner/admin 接受调用。</summary>
public sealed class WebNavigationPreferencesUpdateRequest
{
    /// <summary>目标角色：<c>owner</c>/<c>admin</c>/<c>member</c>/<c>viewer</c>。</summary>
    [Required, StringLength(16), Description("目标角色：owner/admin/member/viewer。")]
    public string TargetRole { get; init; } = null!;

    /// <summary>要写入的偏好项；非白名单 route_key 返 422。</summary>
    [Required, MinLength(1), MaxLength(64), Description("要写入的偏好项；非白名单 route_key 返 422。")]
    public IReadOnlyList<WebNavigationPreferenceUpdateItem> Items { get; init; } = Array.Empty<WebNavigationPreferenceUpdateItem>();
}
