namespace HomeMind.Common.Infrastructure.Constants;

/// <summary>V2.4 B19 发布：Web 端已发布的 <c>route_key</c> 静态白名单；前端菜单按此消费，后端导航偏好服务以此校验。</summary>
public static class NexusWebNavigationKeys
{
    /// <summary>家庭工作台入口。</summary>
    public const string TenantDashboard = "tenant.dashboard";
    /// <summary>确认中心列表与批量确认入口。</summary>
    public const string TenantConfirmations = "tenant.confirmations";
    /// <summary>管家动态时间线与撤销入口。</summary>
    public const string TenantSteward = "tenant.steward";
    /// <summary>家庭知识库 CRUD 入口。</summary>
    public const string TenantKnowledge = "tenant.knowledge";
    /// <summary>家庭成员与生命周期入口。</summary>
    public const string TenantFamily = "tenant.family";
    /// <summary>个人偏好收藏入口。</summary>
    public const string TenantLife = "tenant.life";
    /// <summary>家庭级连接器与成员授权入口（仅开发端 owner/admin）。</summary>
    public const string TenantConnectors = "tenant.connectors";
    /// <summary>个人连接器 OAuth 授权入口（用户端所有成员）。</summary>
    public const string TenantConnectorAuthorize = "tenant.connector.authorize";

    /// <summary>已发布 route_key 的显示顺序；单一真相源，UI 不得覆盖默认 sort_order。</summary>
    public static readonly IReadOnlyList<KeySort> All = new[]
    {
        new KeySort(TenantDashboard, 100),
        new KeySort(TenantConfirmations, 110),
        new KeySort(TenantSteward, 120),
        new KeySort(TenantKnowledge, 130),
        new KeySort(TenantFamily, 140),
        new KeySort(TenantLife, 150),
        new KeySort(TenantConnectors, 160),
        new KeySort(TenantConnectorAuthorize, 170)
    };

    /// <summary>route_key 到默认 sort_order 的查找表；B19 偏好未持久化时使用此默认值。</summary>
    public static readonly IReadOnlyDictionary<string, int> DefaultSortOrder =
        All.ToDictionary(x => x.RouteKey, x => x.SortOrder, StringComparer.Ordinal);

    /// <summary>校验 <paramref name="routeKey"/> 是否命中已发布白名单。</summary>
    /// <param name="routeKey">前端提交的 route_key。</param>
    /// <returns>命中返回 true。</returns>
    public static bool IsKnownRouteKey(string routeKey) => DefaultSortOrder.ContainsKey(routeKey);

    /// <summary>route_key 与默认 sort_order 配对。</summary>
    /// <param name="RouteKey">已发布的路由键。</param>
    /// <param name="SortOrder">默认显示顺序。</param>
    public sealed record KeySort(string RouteKey, int SortOrder);
}
