using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeMind.Common.Model.ViewModel.Data.SmartHome;

public sealed class CreateConnectorRequest
{
    [Required]
    public long? ProviderId { get; init; }

    [Required, StringLength(128)]
    public string? Name { get; init; }

    [Required, StringLength(512)]
    public string? CredentialRef { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnsupportedProperties { get; init; }
}

public sealed class ConnectorAuthorizationRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<string>? Scopes { get; init; }
}
