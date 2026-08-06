namespace HomeMind.CreatorMcp;

internal sealed class CreatorCenterOptions
{
    public string ApiBaseUrl { get; init; } = "http://localhost:5280";
    public string? AccessToken { get; init; }
    public string DatabasePath { get; init; } = Path.Combine(AppContext.BaseDirectory, "data", "creator-center.db");
    public bool AllowSensitiveData { get; init; }

    public static CreatorCenterOptions FromEnvironment() => new()
    {
        ApiBaseUrl = (Environment.GetEnvironmentVariable("NEXUSMIND_API_BASE_URL") ?? "http://localhost:5280").TrimEnd('/'),
        AccessToken = Environment.GetEnvironmentVariable("NEXUSMIND_ACCESS_TOKEN"),
        DatabasePath = Environment.GetEnvironmentVariable("NEXUSMIND_LOCAL_DB_PATH") ?? Path.Combine(AppContext.BaseDirectory, "data", "creator-center.db"),
        AllowSensitiveData = bool.TryParse(Environment.GetEnvironmentVariable("NEXUSMIND_MCP_ALLOW_SENSITIVE"), out var enabled) && enabled
    };
}
