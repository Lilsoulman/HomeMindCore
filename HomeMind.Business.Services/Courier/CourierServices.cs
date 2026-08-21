using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.Courier;
using HomeMind.Common.Model.Entities.Courier;
using HomeMind.Common.Model.Entities.Steward;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Courier;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Courier;

/// <summary>快递管家实现；个人运单按用户隔离，状态同步经快递100 MCP 完成。</summary>
public sealed class CourierServices : ICourierServices
{
    private readonly HomeMindDbContext _db;
    private readonly IKuaidi100McpClient _mcp;
    private static readonly ConcurrentDictionary<string, string> TrackingNumbers = new(StringComparer.Ordinal);
    /// <summary>构造快递管家服务。</summary>
    public CourierServices(HomeMindDbContext db, IKuaidi100McpClient mcp) { _db = db; _mcp = mcp; }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long homeId, long ownerUserId, CourierShipmentCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TrackingNumber) || request.TrackingNumber.Trim().Length is < 6 or > 64)
            return new ServiceResult(422, "trackingNumber 必须为 6-64 个字符。");
        if (request.Carrier?.Length > 64 || request.Label?.Length > 128) return new ServiceResult(422, "carrier 或 label 超出长度限制。");
        var tracking = request.TrackingNumber.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tracking))).ToLowerInvariant();
        TrackingNumbers[$"{homeId}:{ownerUserId}:{hash}"] = tracking;
        var existing = await _db.CourierShipments.SingleOrDefaultAsync(item => item.HomeId == homeId && item.OwnerUserId == ownerUserId && item.TrackingNumberHash == hash, cancellationToken);
        if (existing is not null) return new ServiceResult(200, "运单已登记。", ToView(existing));
        var now = DateTime.UtcNow;
        var shipment = new CourierShipment { HomeId = homeId, OwnerUserId = ownerUserId, TrackingNumberHash = hash, TrackingNumberMasked = Mask(tracking), Carrier = request.Carrier?.Trim(), Label = request.Label?.Trim(), IsFreshFood = request.IsFreshFood, ExpectedDeliveryAt = request.ExpectedDeliveryAt, CreatedAt = now, UpdatedAt = now };
        _db.CourierShipments.Add(shipment);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "运单登记成功。", ToView(shipment));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAsync(long homeId, long ownerUserId, CancellationToken cancellationToken = default) =>
        new(200, "查询成功。", await _db.CourierShipments.Where(item => item.HomeId == homeId && item.OwnerUserId == ownerUserId).OrderByDescending(item => item.UpdatedAt).Select(ToViewExpression()).ToListAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<ServiceResult> RefreshAsync(long homeId, long ownerUserId, long shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.CourierShipments.SingleOrDefaultAsync(item => item.Id == shipmentId && item.HomeId == homeId && item.OwnerUserId == ownerUserId, cancellationToken);
        if (shipment is null) return new ServiceResult(404, "运单不存在。");
        if (!TrackingNumbers.TryGetValue($"{homeId}:{ownerUserId}:{shipment.TrackingNumberHash}", out var trackingNumber)) return new ServiceResult(409, "运单凭据仅在当前受控连接器会话中可用，请重新登记运单后刷新。");
        Kuaidi100TrackingResult result;
        try
        {
            result = await _mcp.TrackAsync(trackingNumber, shipment.Carrier, cancellationToken);
        }
        catch (McpClientException)
        {
            return new ServiceResult(502, "快递服务暂时不可用，请稍后重试。");
        }
        var newEvents = new List<CourierShipmentEventView>();
        foreach (var item in result.Events.OrderBy(item => item.OccurredAt))
        {
            var duplicate = await _db.CourierShipmentEvents.AnyAsync(existing => existing.ShipmentId == shipment.Id && existing.Status == item.Status && existing.OccurredAt == item.OccurredAt && existing.Description == item.Description, cancellationToken);
            if (duplicate) continue;
            var entity = new CourierShipmentEvent { ShipmentId = shipment.Id, Status = NormalizeStatus(item.Status), Description = item.Description.Trim(), Location = item.Location?.Trim(), OccurredAt = item.OccurredAt.ToUniversalTime(), CreatedAt = DateTime.UtcNow };
            _db.CourierShipmentEvents.Add(entity);
            newEvents.Add(new CourierShipmentEventView(entity.Status, entity.Description, entity.Location, entity.OccurredAt));
        }
        await _db.SaveChangesAsync(cancellationToken);
        var latest = await _db.CourierShipmentEvents.Where(item => item.ShipmentId == shipment.Id).OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest is not null) { shipment.LatestStatus = latest.Status; shipment.LatestDescription = latest.Description; shipment.LatestLocation = latest.Location; shipment.LatestEventAt = latest.OccurredAt; }
        shipment.LastCheckedAt = DateTime.UtcNow; shipment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        var anomalies = await DetectAnomaliesAsync(shipment, cancellationToken);
        return new ServiceResult(200, "运单状态已更新。", new CourierRefreshView(ToView(shipment), newEvents, anomalies));
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ListAnomaliesAsync(long homeId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var shipments = await _db.CourierShipments.Where(item => item.HomeId == homeId && item.OwnerUserId == ownerUserId && item.LatestStatus != CourierShipmentStatuses.Delivered).ToListAsync(cancellationToken);
        var result = new List<CourierAnomalyView>();
        foreach (var shipment in shipments) result.AddRange(await DetectAnomaliesAsync(shipment, cancellationToken));
        return new ServiceResult(200, "查询成功。", result);
    }

    private async Task<IReadOnlyList<CourierAnomalyView>> DetectAnomaliesAsync(CourierShipment shipment, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var anomalies = new List<(string Type, string Title, string Description, string Action)>();
        if (shipment.LatestEventAt is { } eventAt && now - eventAt > TimeSpan.FromHours(48) && shipment.LatestStatus is not CourierShipmentStatuses.Delivered)
            anomalies.Add((CourierAnomalyTypes.Stagnant, "物流长时间未更新", $"运单 {shipment.TrackingNumberMasked} 已超过 48 小时没有新的物流记录。", "联系承运商催件"));
        if (shipment.LatestStatus == CourierShipmentStatuses.OutForDelivery && shipment.ExpectedDeliveryAt is { } expected && expected.Date <= now.Date)
            anomalies.Add((CourierAnomalyTypes.Unattended, "包裹可能无人签收", $"运单 {shipment.TrackingNumberMasked} 正在派送，预计今天送达。", "确认改投附近驿站"));
        if (shipment.IsFreshFood && shipment.ExpectedDeliveryAt is { } freshExpected && freshExpected < now && shipment.LatestStatus is not CourierShipmentStatuses.Delivered)
            anomalies.Add((CourierAnomalyTypes.FreshFoodRisk, "生鲜包裹存在时效风险", $"生鲜运单 {shipment.TrackingNumberMasked} 已超过预计送达时间。", "联系承运商优先派送"));
        var views = new List<CourierAnomalyView>();
        foreach (var anomaly in anomalies)
        {
            var existing = await _db.ConfirmationItems.SingleOrDefaultAsync(item => item.HomeId == shipment.HomeId && item.Title == anomaly.Title + " · " + shipment.TrackingNumberMasked && item.Status == ConfirmationItemStatus.Pending, cancellationToken);
            long? confirmationId = existing?.Id;
            if (confirmationId is null)
            {
                var confirmation = new ConfirmationItem { HomeId = shipment.HomeId, RiskLevel = ConfirmationRiskLevel.L1, Title = anomaly.Title + " · " + shipment.TrackingNumberMasked, Description = anomaly.Description, ImpactSummary = "仅创建站内建议，不会自动联系承运商或修改配送地址。", SuggestedAction = anomaly.Action, Status = ConfirmationItemStatus.Pending, ExpiresAt = now.AddDays(2), CreatedAt = now, UpdatedAt = now };
                _db.ConfirmationItems.Add(confirmation); await _db.SaveChangesAsync(cancellationToken); confirmationId = confirmation.Id;
            }
            views.Add(new CourierAnomalyView(shipment.Id, anomaly.Type, anomaly.Title, anomaly.Description, anomaly.Action, confirmationId));
        }
        return views;
    }

    private static string NormalizeStatus(string value) => value.Trim().ToLowerInvariant() switch { "delivered" or "signed" or "已签收" => CourierShipmentStatuses.Delivered, "out_for_delivery" or "派送中" => CourierShipmentStatuses.OutForDelivery, "exception" or "异常" => CourierShipmentStatuses.Exception, _ => CourierShipmentStatuses.InTransit };
    private static string Mask(string value) => value.Length <= 4 ? "****" : new string('*', Math.Max(4, value.Length - 4)) + value[^4..];
    private static CourierShipmentView ToView(CourierShipment item) => new(item.Id, item.TrackingNumberMasked, item.Carrier, item.Label, item.IsFreshFood, item.ExpectedDeliveryAt, item.LatestStatus, item.LatestDescription, item.LatestLocation, item.LatestEventAt, item.LastCheckedAt);
    private static System.Linq.Expressions.Expression<Func<CourierShipment, CourierShipmentView>> ToViewExpression() => item => new CourierShipmentView(item.Id, item.TrackingNumberMasked, item.Carrier, item.Label, item.IsFreshFood, item.ExpectedDeliveryAt, item.LatestStatus, item.LatestDescription, item.LatestLocation, item.LatestEventAt, item.LastCheckedAt);
}
