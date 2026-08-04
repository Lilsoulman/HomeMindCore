using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities;

/// <summary>系统账户表，保存不敏感的用户资料。</summary>
[Table("users")]
public sealed class User
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = "HomeMind 用户";
    [Column("avatar_url")] public string? AvatarUrl { get; set; }
    [Column("status")] public string Status { get; set; } = "active";
    [Column("timezone")] public string Timezone { get; set; } = "Asia/Shanghai";
    [Column("locale")] public string Locale { get; set; } = "zh-CN";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
}

/// <summary>登录标识表，仅保存不可逆标识摘要与可选密文。</summary>
[Table("user_identities")]
public sealed class UserIdentity
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("provider")] public string Provider { get; set; } = null!;
    [Column("issuer")] public string Issuer { get; set; } = null!;
    [Column("subject_kind")] public string SubjectKind { get; set; } = null!;
    [Column("subject_hash")] public byte[] SubjectHash { get; set; } = null!;
    /// <summary>用于管理后台展示的十六进制摘要，不写入数据库。</summary>
    [NotMapped] public string SubjectHashHex => Convert.ToHexString(SubjectHash);
    [Column("subject_encrypted")] public byte[]? SubjectEncrypted { get; set; }
    [Column("is_primary")] public bool IsPrimary { get; set; }
    [Column("verified_at")] public DateTime VerifiedAt { get; set; }
    [Column("last_used_at")] public DateTime? LastUsedAt { get; set; }
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>密码凭据表，仅保存 PBKDF2 哈希值。</summary>
[Table("password_credentials")]
public sealed class PasswordCredential
{
    [Key, Column("user_id")] public long UserId { get; set; }
    [Column("password_hash")] public string PasswordHash { get; set; } = null!;
    [Column("password_changed_at")] public DateTime PasswordChangedAt { get; set; }
    [Column("failed_attempts")] public short FailedAttempts { get; set; }
    [Column("locked_until")] public DateTime? LockedUntil { get; set; }
}

[Table("auth_devices")]
public sealed class AuthDevice
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("installation_id")] public string InstallationId { get; set; } = null!;
    [Column("platform")] public string Platform { get; set; } = "h5";
    [Column("device_name")] public string? DeviceName { get; set; }
    [Column("app_version")] public string? AppVersion { get; set; }
    [Column("last_seen_at")] public DateTime LastSeenAt { get; set; }
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("auth_refresh_tokens")]
public sealed class AuthRefreshToken
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("device_id")] public long DeviceId { get; set; }
    [Column("family_id")] public string FamilyId { get; set; } = null!;
    [Column("token_hash")] public byte[] TokenHash { get; set; } = null!;
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("last_used_at")] public DateTime? LastUsedAt { get; set; }
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
    [Column("revoke_reason")] public string? RevokeReason { get; set; }
    [Column("replaced_by_id")] public long? ReplacedById { get; set; }
}

[Table("tenants")]
public sealed class Tenant
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("tenant_type")] public string TenantType { get; set; } = "personal";
    [Column("code")] public string Code { get; set; } = null!;
    [Column("name")] public string Name { get; set; } = null!;
    [Column("status")] public string Status { get; set; } = "active";
    [Column("owner_user_id")] public long? OwnerUserId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("tenant_members")]
public sealed class TenantMember
{
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("user_id")] public long UserId { get; set; }
    [Column("role")] public string Role { get; set; } = "member";
    [Column("status")] public string Status { get; set; } = "active";
    [Column("joined_at")] public DateTime JoinedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("auth_access_token_revocations")]
public sealed class AccessTokenRevocation
{
    [Key, Column("token_id")] public string TokenId { get; set; } = null!;
    [Column("user_id")] public long UserId { get; set; }
    [Column("tenant_id")] public long TenantId { get; set; }
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("revoked_at")] public DateTime RevokedAt { get; set; }
    [Column("revoke_reason")] public string RevokeReason { get; set; } = "logout";
}
