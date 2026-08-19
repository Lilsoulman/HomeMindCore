using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Finance;

/// <summary>家庭缴费账户，保存到期日和本地登记来源，不保存账户号码或第三方凭据。</summary>
[Table("billing_accounts")]
public sealed class BillingAccount
{
    /// <summary>缴费账户主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键，由 JWT 租户推导。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>创建建档的用户主键。</summary>
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    /// <summary>缴费类别，参见 <see cref="BillingTypes"/>。</summary>
    [Column("billing_type", TypeName = "varchar(32)")] public string BillingType { get; set; } = null!;
    /// <summary>缴费机构的展示名称。</summary>
    [Column("provider", TypeName = "varchar(128)")] public string Provider { get; set; } = null!;
    /// <summary>家庭内用于区分账单的非敏感标签。</summary>
    [Column("label", TypeName = "varchar(128)")] public string Label { get; set; } = null!;
    /// <summary>账单周期月数，用于缴后推算下次到期日。</summary>
    [Column("billing_cycle_months")] public int BillingCycleMonths { get; set; } = 1;
    /// <summary>预计应缴金额，未知时为空。</summary>
    [Column("expected_amount", TypeName = "decimal(18,2)")] public decimal? ExpectedAmount { get; set; }
    /// <summary>金额货币代码。</summary>
    [Column("currency", TypeName = "varchar(8)")] public string Currency { get; set; } = "CNY";
    /// <summary>当前待缴账单的到期日期。</summary>
    [Column("next_due_date", TypeName = "date")] public DateTime NextDueDate { get; set; }
    /// <summary>建档来源，参见 <see cref="BillingSourceTypes"/>。</summary>
    [Column("source_type", TypeName = "varchar(16)")] public string SourceType { get; set; } = BillingSourceTypes.Manual;
    /// <summary>本地文件或解析批次的脱敏引用，不包含原始缴费单内容。</summary>
    [Column("source_ref", TypeName = "varchar(256)")] public string? SourceRef { get; set; }
    /// <summary>是否继续参与到期提醒。</summary>
    [Column("is_active")] public bool IsActive { get; set; } = true;
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>最近更新时间。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>家庭缴费完成记录，并关联同步写入的财务事实条目。</summary>
[Table("billing_payment_records")]
public sealed class BillingPaymentRecord
{
    /// <summary>缴费记录主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属缴费账户主键。</summary>
    [Column("billing_account_id")] public long BillingAccountId { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>登记缴费的用户主键。</summary>
    [Column("recorded_by_user_id")] public long RecordedByUserId { get; set; }
    /// <summary>本次账单到期日期。</summary>
    [Column("due_date", TypeName = "date")] public DateTime DueDate { get; set; }
    /// <summary>实际缴费日期。</summary>
    [Column("paid_at", TypeName = "date")] public DateTime PaidAt { get; set; }
    /// <summary>实际缴费金额。</summary>
    [Column("amount", TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    /// <summary>金额货币代码。</summary>
    [Column("currency", TypeName = "varchar(8)")] public string Currency { get; set; } = "CNY";
    /// <summary>登记来源，参见 <see cref="BillingSourceTypes"/>。</summary>
    [Column("source_type", TypeName = "varchar(16)")] public string SourceType { get; set; } = BillingSourceTypes.Manual;
    /// <summary>关联的财务账单事实主键。</summary>
    [Column("finance_transaction_id")] public long FinanceTransactionId { get; set; }
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>缴费类别常量。</summary>
public static class BillingTypes
{
    /// <summary>水费。</summary>
    public const string Water = "water";
    /// <summary>电费。</summary>
    public const string Electricity = "electricity";
    /// <summary>燃气费。</summary>
    public const string Gas = "gas";
    /// <summary>物业费。</summary>
    public const string Property = "property";
    /// <summary>话费或通信费。</summary>
    public const string Mobile = "mobile";
    /// <summary>保险费。</summary>
    public const string Insurance = "insurance";
    /// <summary>其他周期缴费。</summary>
    public const string Other = "other";

    /// <summary>全部允许的缴费类别。</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    { Water, Electricity, Gas, Property, Mobile, Insurance, Other };
}

/// <summary>缴费建档与记录来源常量。</summary>
public static class BillingSourceTypes
{
    /// <summary>用户手动输入。</summary>
    public const string Manual = "manual";
    /// <summary>用户设备本地 OCR 提取后提交的结构化字段。</summary>
    public const string Ocr = "ocr";
    /// <summary>由既有财务账单事实辅助建档。</summary>
    public const string Finance = "finance";

    /// <summary>全部允许的来源类型。</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    { Manual, Ocr, Finance };
}
