using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Courier;

/// <summary>个人快递运单，凭运单哈希和脱敏尾号关联，不保存完整运单号。</summary>
[Table("courier_shipments")]
public sealed class CourierShipment
{
    /// <summary>运单主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>个人连接所有者用户主键。</summary>
    [Column("owner_user_id")] public long OwnerUserId { get; set; }
    /// <summary>完整运单号的不可逆哈希。</summary>
    [Column("tracking_number_hash", TypeName = "char(64)")] public string TrackingNumberHash { get; set; } = null!;
    /// <summary>用于识别运单的脱敏尾号。</summary>
    [Column("tracking_number_masked", TypeName = "varchar(32)")] public string TrackingNumberMasked { get; set; } = null!;
    /// <summary>承运商展示名称。</summary>
    [Column("carrier", TypeName = "varchar(64)")] public string? Carrier { get; set; }
    /// <summary>用户自定义包裹标签。</summary>
    [Column("label", TypeName = "varchar(128)")] public string? Label { get; set; }
    /// <summary>是否为生鲜包裹。</summary>
    [Column("is_fresh_food")] public bool IsFreshFood { get; set; }
    /// <summary>预计送达时间，未知时为空。</summary>
    [Column("expected_delivery_at")] public DateTime? ExpectedDeliveryAt { get; set; }
    /// <summary>最近一次快递状态编码。</summary>
    [Column("latest_status", TypeName = "varchar(32)")] public string LatestStatus { get; set; } = CourierShipmentStatuses.Unknown;
    /// <summary>最近一次状态描述。</summary>
    [Column("latest_description", TypeName = "varchar(512)")] public string? LatestDescription { get; set; }
    /// <summary>最近一次状态地点。</summary>
    [Column("latest_location", TypeName = "varchar(128)")] public string? LatestLocation { get; set; }
    /// <summary>最近一条物流事件发生时间。</summary>
    [Column("latest_event_at")] public DateTime? LatestEventAt { get; set; }
    /// <summary>最近一次向快递服务查询时间。</summary>
    [Column("last_checked_at")] public DateTime? LastCheckedAt { get; set; }
    /// <summary>创建时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>快递状态事件事实源。</summary>
[Table("courier_shipment_events")]
public sealed class CourierShipmentEvent
{
    /// <summary>事件主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属运单主键。</summary>
    [Column("shipment_id")] public long ShipmentId { get; set; }
    /// <summary>状态编码。</summary>
    [Column("status", TypeName = "varchar(32)")] public string Status { get; set; } = CourierShipmentStatuses.Unknown;
    /// <summary>物流描述。</summary>
    [Column("description", TypeName = "varchar(512)")] public string Description { get; set; } = null!;
    /// <summary>物流地点。</summary>
    [Column("location", TypeName = "varchar(128)")] public string? Location { get; set; }
    /// <summary>事件发生时间。</summary>
    [Column("occurred_at")] public DateTime OccurredAt { get; set; }
    /// <summary>写入时间。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>快递状态编码集合。</summary>
public static class CourierShipmentStatuses
{
    /// <summary>未知状态。</summary>
    public const string Unknown = "unknown";
    /// <summary>运输中。</summary>
    public const string InTransit = "in_transit";
    /// <summary>派送中。</summary>
    public const string OutForDelivery = "out_for_delivery";
    /// <summary>已签收。</summary>
    public const string Delivered = "delivered";
    /// <summary>异常。</summary>
    public const string Exception = "exception";
}

/// <summary>快递异常类型集合。</summary>
public static class CourierAnomalyTypes
{
    /// <summary>物流长时间没有更新。</summary>
    public const string Stagnant = "stagnant";
    /// <summary>派送中但可能无人签收。</summary>
    public const string Unattended = "unattended";
    /// <summary>生鲜包裹存在时效风险。</summary>
    public const string FreshFoodRisk = "fresh_food_risk";
}
