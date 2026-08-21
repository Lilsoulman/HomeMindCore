namespace HomeMind.Common.Model.ViewModel.Data.Courier;

/// <summary>登记个人快递运单请求。</summary>
public sealed record CourierShipmentCreateRequest(string TrackingNumber, string? Carrier = null, string? Label = null, bool IsFreshFood = false, DateTime? ExpectedDeliveryAt = null);

/// <summary>快递运单对外视图，不返回完整运单号或第三方凭据。</summary>
public sealed record CourierShipmentView(long Id, string TrackingNumberMasked, string? Carrier, string? Label, bool IsFreshFood, DateTime? ExpectedDeliveryAt, string LatestStatus, string? LatestDescription, string? LatestLocation, DateTime? LatestEventAt, DateTime? LastCheckedAt);

/// <summary>快递状态事件对外视图。</summary>
public sealed record CourierShipmentEventView(string Status, string Description, string? Location, DateTime OccurredAt);

/// <summary>快递异常建议视图。</summary>
public sealed record CourierAnomalyView(long ShipmentId, string Type, string Title, string Description, string SuggestedAction, long? ConfirmationId);

/// <summary>快递刷新结果。</summary>
public sealed record CourierRefreshView(CourierShipmentView Shipment, IReadOnlyList<CourierShipmentEventView> NewEvents, IReadOnlyList<CourierAnomalyView> Anomalies);
