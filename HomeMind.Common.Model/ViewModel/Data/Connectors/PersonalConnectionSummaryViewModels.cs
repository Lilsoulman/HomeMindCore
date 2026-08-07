using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HomeMind.Common.Model.ViewModel.Data.Connectors;

/// <summary>"我的个人连接"汇总视图：连接实例 + 最近一次授权会话状态，不返回凭据引用。</summary>
/// <param name="ConnectorId">工作区连接器主键。</param>
/// <param name="ProviderId">提供方主键。</param>
/// <param name="ProviderCode">提供方编码。</param>
/// <param name="ProviderName">提供方展示名。</param>
/// <param name="Name">租户侧自定义的连接器名称。</param>
/// <param name="Status">连接器运行状态（connected/disconnected 等）。</param>
/// <param name="AuthStatus">授权生命周期状态（none/authorizing/connected/revoked/failed）。</param>
/// <param name="LastSyncAt">最近一次状态同步时间（UTC）。</param>
/// <param name="LastHealthAt">最近一次健康探测时间（UTC）。</param>
/// <param name="LastSessionId">最近一次授权会话主键，可空（尚未发起过 OAuth）。</param>
/// <param name="LastSessionStatus">最近一次授权会话状态（pending/used/expired/revoked/completed/failed），可空。</param>
/// <param name="LastSessionExpiresAt">最近一次授权会话过期时间（UTC），可空。</param>
public sealed record PersonalConnectionSummaryView(
    long ConnectorId,
    long ProviderId,
    string ProviderCode,
    string ProviderName,
    string Name,
    string Status,
    string AuthStatus,
    DateTime? LastSyncAt,
    DateTime? LastHealthAt,
    long? LastSessionId,
    string? LastSessionStatus,
    DateTime? LastSessionExpiresAt);
