using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeMind.Common.Model.ViewModel.Data.SmartHome;

/// <summary>创建工作区连接器的请求体。</summary>
public sealed class CreateConnectorRequest
{
    /// <summary>提供方主键，必填。</summary>
    [Required, Description("连接器提供方主键，可通过 GET /api/v1/connector-providers 获取。")]
    public long? ProviderId { get; init; }

    /// <summary>租户侧自定义的连接器名称，最长 128 字符。</summary>
    [Required, StringLength(128), Description("租户侧自定义的连接器展示名，长度 1-128。")]
    public string? Name { get; init; }

    /// <summary>凭据引用，格式必须为 vault://tenants/{tenantId}/...，API 不会返回明文。</summary>
    [Required, StringLength(512), Description("租户拥有的凭据引用，格式为 vault://tenants/{tenantId}/...，创建后永不返回明文。")]
    public string? CredentialRef { get; init; }

    /// <summary>暂未被识别的扩展字段，将原样回传以避免前端误传。</summary>
    [JsonExtensionData, Description("未识别的扩展字段，原样保留以便回传给客户端。")]
    public Dictionary<string, JsonElement>? UnsupportedProperties { get; init; }
}

/// <summary>对工作区连接器进行范围授权的请求体。</summary>
public sealed class ConnectorAuthorizationRequest
{
    /// <summary>授权范围列表，至少 1 项。</summary>
    [Required, MinLength(1), Description("授权范围字符串列表，至少包含 1 项。")]
    public IReadOnlyList<string>? Scopes { get; init; }
}
