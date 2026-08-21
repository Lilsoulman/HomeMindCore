using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Pet;

/// <summary>家庭宠物档案，保存照护所需的非敏感基础信息。</summary>
[Table("pet_profiles")]
public sealed class PetProfile
{
    /// <summary>宠物档案主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>创建档案的用户主键。</summary>
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    /// <summary>宠物昵称。</summary>
    [Column("name", TypeName = "varchar(64)")] public string Name { get; set; } = null!;
    /// <summary>宠物种类，例如猫或狗。</summary>
    [Column("species", TypeName = "varchar(32)")] public string Species { get; set; } = null!;
    /// <summary>品种，可为空。</summary>
    [Column("breed", TypeName = "varchar(64)")] public string? Breed { get; set; }
    /// <summary>出生日期，可为空。</summary>
    [Column("birth_date", TypeName = "date")] public DateTime? BirthDate { get; set; }
    /// <summary>备注，不用于推理或共享。</summary>
    [Column("notes", TypeName = "varchar(512)")] public string? Notes { get; set; }
    /// <summary>是否仍参与家庭提醒。</summary>
    [Column("is_active")] public bool IsActive { get; set; } = true;
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>宠物疫苗、驱虫等照护日历记录。</summary>
[Table("pet_care_events")]
public sealed class PetCareEvent
{
    /// <summary>照护记录主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属宠物主键。</summary>
    [Column("pet_id")] public long PetId { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>照护类型，取 vaccine 或 deworming。</summary>
    [Column("care_type", TypeName = "varchar(16)")] public string CareType { get; set; } = null!;
    /// <summary>照护项目名称。</summary>
    [Column("title", TypeName = "varchar(128)")] public string Title { get; set; } = null!;
    /// <summary>下一次到期日期。</summary>
    [Column("due_date", TypeName = "date")] public DateTime DueDate { get; set; }
    /// <summary>完成时间，未完成时为空。</summary>
    [Column("completed_at", TypeName = "date")] public DateTime? CompletedAt { get; set; }
    /// <summary>备注。</summary>
    [Column("notes", TypeName = "varchar(512)")] public string? Notes { get; set; }
    /// <summary>创建记录的用户主键。</summary>
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>宠物用品库存与日均消耗记录。</summary>
[Table("pet_supply_records")]
public sealed class PetSupplyRecord
{
    /// <summary>用品记录主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属宠物主键。</summary>
    [Column("pet_id")] public long PetId { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>用品名称。</summary>
    [Column("item_name", TypeName = "varchar(128)")] public string ItemName { get; set; } = null!;
    /// <summary>当前库存数量。</summary>
    [Column("quantity", TypeName = "decimal(18,3)")] public decimal Quantity { get; set; }
    /// <summary>日均消耗数量。</summary>
    [Column("daily_usage", TypeName = "decimal(18,3)")] public decimal DailyUsage { get; set; }
    /// <summary>数量单位。</summary>
    [Column("unit", TypeName = "varchar(16)")] public string Unit { get; set; } = "份";
    /// <summary>库存来源，取 manual 或 finance。</summary>
    [Column("source_type", TypeName = "varchar(16)")] public string SourceType { get; set; } = PetSupplySourceTypes.Manual;
    /// <summary>最近一次更新库存的日期。</summary>
    [Column("measured_at", TypeName = "date")] public DateTime MeasuredAt { get; set; }
    /// <summary>创建记录的用户主键。</summary>
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    /// <summary>更新时间。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>宠物用品库存来源。</summary>
public static class PetSupplySourceTypes
{
    /// <summary>用户手动记录。</summary>
    public const string Manual = "manual";
    /// <summary>由家庭财务消费事实辅助记录。</summary>
    public const string Finance = "finance";
    /// <summary>允许的来源。</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Manual, Finance };
}

/// <summary>宠物照护类型。</summary>
public static class PetCareTypes
{
    /// <summary>疫苗接种。</summary>
    public const string Vaccine = "vaccine";
    /// <summary>驱虫。</summary>
    public const string Deworming = "deworming";
    /// <summary>允许的照护类型。</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Vaccine, Deworming };
}
