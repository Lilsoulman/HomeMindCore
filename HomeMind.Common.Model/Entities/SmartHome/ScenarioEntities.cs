using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.SmartHome;

/// <summary>平台级场景模板，由平台定义能力模板；家庭启用后生成实例，不直接执行。</summary>
[Table("scenario_templates")]
public sealed class ScenarioTemplate
{
    /// <summary>模板主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>模板归属租户，平台模板固定为 1，与平台专家同惯例。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>模板业务键，全局唯一，如 goodnight / arrive_home / leave_home。</summary>
    [Column("code")] public string Code { get; set; } = null!;
    /// <summary>模板展示名。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>模板摘要。</summary>
    [Column("summary")] public string? Summary { get; set; }
    /// <summary>模板状态，参见 <see cref="ScenarioTemplateStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "active";
    /// <summary>触发关键词 JSON 数组，语音入口按关键词确定性匹配。</summary>
    [Column("trigger_keywords_json")] public string? TriggerKeywords { get; set; }
    /// <summary>模板步骤 JSON 数组，步骤未解析设备：id/name/device_type/room/capability/value/optional。</summary>
    [Column("steps_json")] public string Steps { get; set; } = "[]";
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; }
}

/// <summary>家庭启用的场景实例，步骤经 Device Resolver 解析为具体设备并记录可用性。</summary>
[Table("scenario_instances")]
public sealed class ScenarioInstance
{
    /// <summary>实例主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属租户标识。</summary>
    [Column("tenant_id")] public long TenantId { get; set; }
    /// <summary>来源模板业务键。</summary>
    [Column("template_code")] public string TemplateCode { get; set; } = null!;
    /// <summary>实例展示名，启用时取自模板。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>触发关键词快照 JSON 数组。</summary>
    [Column("trigger_keywords_json")] public string? TriggerKeywords { get; set; }
    /// <summary>解析后步骤 JSON 数组：模板字段之外附加 device_id/step_status/reason；step_status 取值参见 <see cref="ScenarioStepStatus"/>。</summary>
    [Column("steps_json")] public string Steps { get; set; } = "[]";
    /// <summary>实例状态，参见 <see cref="ScenarioInstanceStatus"/>。</summary>
    [Column("status")] public string Status { get; set; } = "enabled";
    /// <summary>启用实例的用户标识。</summary>
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
    /// <summary>同步版本号。</summary>
    [Column("sync_version")] public long SyncVersion { get; set; } = 1;
}

/// <summary>场景模板状态集合。</summary>
public static class ScenarioTemplateStatus
{
    /// <summary>可被家庭启用。</summary>
    public const string Active = "active";
    /// <summary>已停用，不再允许新启用。</summary>
    public const string Inactive = "inactive";
}

/// <summary>场景实例状态集合。</summary>
public static class ScenarioInstanceStatus
{
    /// <summary>已启用，可运行。</summary>
    public const string Enabled = "enabled";
    /// <summary>已停用。</summary>
    public const string Disabled = "disabled";
}

/// <summary>实例步骤可用性状态集合。</summary>
public static class ScenarioStepStatus
{
    /// <summary>已解析到具体设备，可执行。</summary>
    public const string Ready = "ready";
    /// <summary>无匹配设备或能力，执行时跳过。</summary>
    public const string Unavailable = "unavailable";
}
