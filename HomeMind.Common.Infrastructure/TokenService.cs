using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Common.Infrastructure;

/// <summary>访问令牌的已验证载荷。</summary>
public sealed record AccessTokenPayload(long UserId, long TenantId, long DeviceId, long ExpiresAtUnixTime, string TokenId);

/// <summary>创建和验证 HomeMind 签名访问令牌。</summary>
public sealed class TokenService
{
    private const string LocalDevelopmentSigningKey = "HomeMind-local-development-signing-key-change-before-production";
    private readonly byte[] _key;
    private readonly int _accessMinutes;
    public int RefreshTokenDays { get; }

    public TokenService(IConfiguration configuration)
    {
        ValidateConfiguration(configuration);
        _key = Encoding.UTF8.GetBytes(configuration["Auth:SigningKey"]!);
        _accessMinutes = ReadPositiveInt(configuration, "Auth:AccessTokenMinutes", 15, 1, 24 * 60);
        RefreshTokenDays = ReadPositiveInt(configuration, "Auth:RefreshTokenDays", 30, 1, 365);
    }

    public static void ValidateConfiguration(IConfiguration configuration, bool production = false)
    {
        var signingKey = configuration["Auth:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new InvalidOperationException("Missing required configuration: Auth:SigningKey.");
        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes.");
        if (production && string.Equals(signingKey, LocalDevelopmentSigningKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Auth:SigningKey must be set to a production secret, not the local development key.");

        _ = ReadPositiveInt(configuration, "Auth:AccessTokenMinutes", 15, 1, 24 * 60);
        _ = ReadPositiveInt(configuration, "Auth:RefreshTokenDays", 30, 1, 365);
    }

    public string CreateAccessToken(long userId, long tenantId, long deviceId)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_accessMinutes).ToUnixTimeSeconds();
        var payload = $"{userId}:{tenantId}:{deviceId}:{expires}:{Guid.NewGuid():N}";
        var payloadText = Base64Url(Encoding.UTF8.GetBytes(payload));
        return $"{payloadText}.{Sign(payloadText)}";
    }

    public bool TryRead(string token, out AccessTokenPayload payload)
    {
        payload = default!;
        var parts = token.Split('.', 2);
        if (parts.Length != 2 || !FixedEquals(Sign(parts[0]), parts[1])) return false;
        try
        {
            var values = Encoding.UTF8.GetString(FromBase64Url(parts[0])).Split(':');
            var isLegacyToken = values.Length == 4;
            if ((!isLegacyToken && values.Length != 5) || !long.TryParse(values[0], out var userId) || !long.TryParse(values[1], out var tenantId)) return false;
            var deviceId = 0L;
            var expiresIndex = isLegacyToken ? 2 : 3;
            var tokenIdIndex = isLegacyToken ? 3 : 4;
            if ((!isLegacyToken && !long.TryParse(values[2], out deviceId)) || !long.TryParse(values[expiresIndex], out var expires) || values[tokenIdIndex].Length != 32) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expires) return false;
            payload = new AccessTokenPayload(userId, tenantId, deviceId, expires, values[tokenIdIndex]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string Sign(string text)
    {
        using var hmac = new HMACSHA256(_key);
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }

    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));

    private static int ReadPositiveInt(IConfiguration configuration, string key, int fallback, int minimum, int maximum)
    {
        var configuredValue = configuration[key];
        if (string.IsNullOrWhiteSpace(configuredValue)) return fallback;
        if (int.TryParse(configuredValue, out var value) && value >= minimum && value <= maximum) return value;
        throw new InvalidOperationException($"{key} must be an integer between {minimum} and {maximum}.");
    }
}
