namespace HomeMind.Business.IServices.Connector;

/// <summary>Home Assistant MCP 运行配置，仅用于在适配器内部选择受控的只读 MCP 路径。</summary>
public sealed class HomeAssistantMcpOptions
{
    /// <summary>运行模式：mcp 使用本地 MCP，rest_fallback 使用既有 REST，disabled 禁用连接器。</summary>
    public string Mode { get; init; } = "rest_fallback";

    /// <summary>MCP 服务名，用于会话缓存与配置定位。</summary>
    public string ServerName { get; init; } = "home-assistant";

    /// <summary>是否启用本地 stdio MCP 进程；关闭时使用确定性 Mock 会话。</summary>
    public bool Enabled { get; init; }

    /// <summary>stdio 进程启动配置；凭据由本地 MCP 进程自行托管，不能写入此配置。</summary>
    public McpProcessOptions Process { get; init; } = new();

    /// <summary>是否启用 Home Assistant WebSocket 实时状态订阅；关闭时不建立后台长连接。</summary>
    public bool EventSubscriptionEnabled { get; init; }

    /// <summary>允许订阅的实体域；实体白名单未配置时仅这些域可进入状态同步。</summary>
    public string[] WatchDomains { get; init; } = ["light", "switch", "climate", "cover", "sensor", "binary_sensor"];

    /// <summary>额外收紧订阅范围的实体白名单；为空时按允许域过滤。</summary>
    public string[] WatchEntities { get; init; } = [];

    /// <summary>始终忽略的实体标识，优先级高于允许域和实体白名单。</summary>
    public string[] IgnoreEntities { get; init; } = [];

    /// <summary>同一实体相同状态的最短写入间隔，单位为秒。</summary>
    public int EventCooldownSeconds { get; init; } = 3;
}
