using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Finance;

/// <summary>家庭财务账单条目，保存本地解析后的最小消费事实。</summary>
[Table("finance_transactions")]
public sealed class FinanceTransaction
{
    /// <summary>账单条目主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键，由 JWT 租户推导。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>导入操作用户主键。</summary>
    [Column("created_by_user_id")] public long CreatedByUserId { get; set; }
    /// <summary>消费发生日期。</summary>
    [Column("transaction_date", TypeName = "date")] public DateTime TransactionDate { get; set; }
    /// <summary>商户或收款方名称。</summary>
    [Column("merchant", TypeName = "varchar(256)")] public string Merchant { get; set; } = null!;
    /// <summary>消费金额，支出为正数。</summary>
    [Column("amount", TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    /// <summary>货币代码，默认人民币。</summary>
    [Column("currency", TypeName = "varchar(8)")] public string Currency { get; set; } = "CNY";
    /// <summary>归类结果，例如餐饮、订阅或交通。</summary>
    [Column("category", TypeName = "varchar(64)")] public string Category { get; set; } = "其他";
    /// <summary>来源类型：csv、ocr 或 manual。</summary>
    [Column("source_type", TypeName = "varchar(16)")] public string SourceType { get; set; } = "csv";
    /// <summary>来源文件或外部流水的去重引用。</summary>
    [Column("source_ref", TypeName = "varchar(256)")] public string? SourceRef { get; set; }
    /// <summary>归一化账单行哈希，用于重复导入去重。</summary>
    [Column("content_hash", TypeName = "char(64)")] public string ContentHash { get; set; } = null!;
    /// <summary>备注或解析说明。</summary>
    [Column("notes", TypeName = "varchar(512)")] public string? Notes { get; set; }
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
