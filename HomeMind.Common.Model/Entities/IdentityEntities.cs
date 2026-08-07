using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities;

/// <summary>系统账户表，保存不敏感的用户资料。</summary>
[Table("users")]
public sealed class User
{
    /// <summary>用户主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>用户对外展示名称。</summary>
    [Column("display_name")] public string DisplayName { get; set; } = "HomeMind 用户";
    /// <summary>用户头像 URL，可为空。</summary>
    [Column("avatar_url")] public string? AvatarUrl { get; set; }
    /// <summary>账户状态，参见 <see cref="UserStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>用户默认时区，使用 IANA 时区标识。</summary>
    [Column("timezone")] public string Timezone { get; set; } = "Asia/Shanghai";
    /// <summary>用户偏好语言标签，遵循 BCP 47。</summary>
    [Column("locale")] public string Locale { get; set; } = "zh-CN";
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳，账户禁用而非物理删除。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

/// <summary>登录标识表，仅保存不可逆标识摘要与可选密文。</summary>
[Table("user_identities")]
public sealed class UserIdentity
{
    /// <summary>登录标识主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>关联用户主键。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>认证提供方，如"phone""wechat"等。</summary>
    [Column("provider")] public string Provider { get; set; } = null!;
    /// <summary>颁发方，便于区分同名提供方下的子渠道。</summary>
    [Column("issuer")] public string Issuer { get; set; } = null!;
    /// <summary>主体类型，如"phone_number""openid"等。</summary>
    [Column("subject_kind")] public string SubjectKind { get; set; } = null!;
    /// <summary>主体 SHA-256 摘要，用于检索去重与不返回明文。</summary>
    [Column("subject_hash")] public byte[] SubjectHash { get; set; } = null!;
    /// <summary>用于管理后台展示的十六进制摘要，不写入数据库。</summary>
    [NotMapped] public string SubjectHashHex => Convert.ToHexString(SubjectHash);
    /// <summary>主体密文，可用于重发登录或回执展示，由 KMS 加密。</summary>
    [Column("subject_encrypted")] public byte[]? SubjectEncrypted { get; set; }
    /// <summary>是否主登录标识，登录时优先使用。</summary>
    [Column("is_primary")] public bool IsPrimary { get; set; }
    /// <summary>最近一次校验通过时间（UTC）。</summary>
    [Column("verified_at")] public DateTime VerifiedAt { get; set; }
    /// <summary>最近一次登录使用时间（UTC）。</summary>
    [Column("last_used_at")] public DateTime? LastUsedAt { get; set; }
    /// <summary>吊销时间（UTC），吊销后禁止使用。</summary>
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>密码凭据表，仅保存 PBKDF2 哈希值。</summary>
[Table("password_credentials")]
public sealed class PasswordCredential
{
    /// <summary>所属用户主键。</summary>
    [Key, Column("user_id")] public long UserId { get; set; }
    /// <summary>PBKDF2 哈希字符串，包含算法参数与盐。</summary>
    [Column("password_hash")] public string PasswordHash { get; set; } = null!;
    /// <summary>最近一次修改密码的时间（UTC）。</summary>
    [Column("password_changed_at")] public DateTime PasswordChangedAt { get; set; }
    /// <summary>连续登录失败计数，超过阈值后锁定。</summary>
    [Column("failed_attempts")] public short FailedAttempts { get; set; }
    /// <summary>账户锁定到期时间（UTC），解锁前拒绝登录。</summary>
    [Column("locked_until")] public DateTime? LockedUntil { get; set; }
}

/// <summary>登录设备表，绑定用户与设备指纹。</summary>
[Table("auth_devices")]
public sealed class AuthDevice
{
    /// <summary>设备主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属用户主键。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>客户端安装 ID，用于关联刷新令牌家族。</summary>
    [Column("installation_id")] public string InstallationId { get; set; } = null!;
    /// <summary>客户端平台，如"ios""android""h5"等。</summary>
    [Column("platform")] public string Platform { get; set; } = "h5";
    /// <summary>设备名称，便于用户在多设备列表中识别。</summary>
    [Column("device_name")] public string? DeviceName { get; set; }
    /// <summary>客户端应用版本字符串。</summary>
    [Column("app_version")] public string? AppVersion { get; set; }
    /// <summary>最近一次活跃时间（UTC），用于列表排序与异常检测。</summary>
    [Column("last_seen_at")] public DateTime LastSeenAt { get; set; }
    /// <summary>吊销时间（UTC），吊销后必须重新登录。</summary>
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>刷新令牌表，仅保存哈希，支持轮换与家族级撤销。</summary>
[Table("auth_refresh_tokens")]
public sealed class AuthRefreshToken
{
    /// <summary>刷新令牌主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属用户主键。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>所属登录设备主键。</summary>
    [Column("device_id")] public long DeviceId { get; set; }
    /// <summary>刷新令牌家族标识，家族内任何令牌被冒用将触发整体撤销。</summary>
    [Column("family_id")] public string FamilyId { get; set; } = null!;
    /// <summary>刷新令牌 SHA-256 摘要，不保存明文。</summary>
    [Column("token_hash")] public byte[] TokenHash { get; set; } = null!;
    /// <summary>过期时间（UTC），到达后必须重新登录。</summary>
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>最近一次使用时间（UTC），用于闲置清理。</summary>
    [Column("last_used_at")] public DateTime? LastUsedAt { get; set; }
    /// <summary>撤销时间（UTC）。</summary>
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    /// <summary>撤销原因，便于审计。</summary>
    [Column("revoke_reason")] public string? RevokeReason { get; set; }
    /// <summary>轮换后被取代的新刷新令牌主键。</summary>
    [Column("replaced_by_id")] public long? ReplacedById { get; set; }
}

/// <summary>租户表，承载多家庭或多组织隔离边界。</summary>
[Table("tenants")]
public sealed class Tenant
{
    /// <summary>租户主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>租户类型，如"personal""family"等。</summary>
    [Column("tenant_type")] public string TenantType { get; set; } = "personal";
    /// <summary>租户业务编码，全局唯一。</summary>
    [Column("code")] public string Code { get; set; } = null!;
    /// <summary>租户对外展示名称。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>租户状态，参见 <see cref="TenantStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>租户所有者用户标识，可为空表示平台维护。</summary>
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>租户成员表，建立用户与租户的多对多关系。</summary>
[Table("tenant_members")]
public sealed class TenantMember
{
    /// <summary>所属租户主键。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>成员用户主键。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>成员角色，如"owner""admin""member"等。</summary>
    [Column("role")] public string Role { get; set; } = "member";
    /// <summary>成员状态。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>加入时间（UTC）。</summary>
    [Column("joined_at")] public DateTime JoinedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号；B19 启用，由 EF <c>IsConcurrencyToken</c> 强制。</summary>
    [ConcurrencyCheck, Column("row_version")] public long RowVersion { get; set; } = 1;
}

/// <summary>家庭成员邀请记录；以手机号 SHA-256 摘要匹配已验证账户，仅允许已 verified 账户接受。</summary>
[Table("tenant_member_invitations")]
public sealed class TenantMemberInvitation
{
    /// <summary>邀请主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭（租户）主键。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>邀请发起人用户主键。</summary>
    [Column("invited_by_user_id")] public long InvitedByUserId { get; set; }
    /// <summary>受邀标识类型，固定为 <c>phone</c>，与 <c>user_identities.subject_kind</c> 对齐。</summary>
    [Column("subject_kind")] public string SubjectKind { get; set; } = "phone";
    /// <summary>手机号 SHA-256 摘要，与 <c>user_identities.subject_hash</c> 同口径（无 pepper）。</summary>
    [Column("subject_hash")] public byte[] SubjectHash { get; set; } = null!;
    /// <summary>接受后授予的角色，固定为 <c>admin</c>/<c>member</c>/<c>viewer</c>，不得为 <c>owner</c>。</summary>
    [Column("proposed_role")] public string ProposedRole { get; set; } = null!;
    /// <summary>状态机：<c>pending</c> / <c>accepted</c> / <c>expired</c> / <c>revoked</c>。</summary>
    [Column("status")] public string Status { get; set; } = "pending";
    /// <summary>邀请过期时间（UTC），默认 7 天；过期按计算语义不写回填。</summary>
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    /// <summary>接受该邀请的用户主键，<c>pending</c> 时为空。</summary>
    [Column("accepted_user_id")] public long? AcceptedUserId { get; set; }
    /// <summary>接受时间（UTC），<c>pending</c> 时为空。</summary>
    [Column("accepted_at")] public DateTime? AcceptedAt { get; set; }
    /// <summary>撤销时间（UTC），<c>pending</c> 时为空。</summary>
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [ConcurrencyCheck, Column("row_version")] public long RowVersion { get; set; } = 1;
}

/// <summary>Web 导航偏好：角色粒度的 route_key 显隐与排序，route_key 须命中后端静态白名单。</summary>
[Table("web_navigation_preferences")]
public sealed class WebNavigationPreference
{
    /// <summary>偏好主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭（租户）主键。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>适用角色：owner / admin / member / viewer，固定枚举。</summary>
    [Column("role")] public string Role { get; set; } = null!;
    /// <summary>已发布的 route_key，由 <c>NexusWebNavigationKeys.All</c> 校验。</summary>
    [Column("route_key")] public string RouteKey { get; set; } = null!;
    /// <summary>是否在菜单中显示。</summary>
    [Column("enabled")] public bool Enabled { get; set; } = true;
    /// <summary>显示顺序；值越小越靠前。</summary>
    [Column("sort_order")] public int SortOrder { get; set; }
    /// <summary>最近一次写入者用户主键。</summary>
    [Column("updated_by_user_id")] public long UpdatedByUserId { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>访问令牌撤销表，存储主动登出或风控触发的 JTI 黑名单。</summary>
[Table("auth_access_token_revocations")]
public sealed class AccessTokenRevocation
{
    /// <summary>被撤销访问令牌的 JTI 标识。</summary>
    [Key, Column("token_id")] public string TokenId { get; set; } = null!;
    /// <summary>令牌所属用户主键。</summary>
    [Column("user_id")] public long UserId { get; set; }
    /// <summary>令牌所属租户主键。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>令牌原过期时间（UTC），用于过期清理。</summary>
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    /// <summary>撤销时间（UTC）。</summary>
    [Column("revoked_at")] public DateTime RevokedAt { get; set; }
    /// <summary>撤销原因，如"logout""password_reset"等。</summary>
    [Column("revoke_reason")] public string RevokeReason { get; set; } = "logout";
}
