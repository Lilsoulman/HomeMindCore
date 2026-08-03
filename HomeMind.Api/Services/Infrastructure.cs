using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace HomeMind.Api.Services;

public sealed class MySqlConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("HomeMind")
            ?? throw new InvalidOperationException("ConnectionStrings:HomeMind is required.");
    }

    public MySqlConnection Open() => new(_connectionString);
}

public sealed record AccessTokenPayload(long UserId, long TenantId, long DeviceId, long ExpiresAtUnixTime, string TokenId);

public sealed record UserContext(long UserId, long TenantId, long DeviceId, string Role, long ExpiresAtUnixTime, string TokenId);

public sealed class TokenService
{
    private readonly byte[] _key;
    private readonly int _accessMinutes;
    public int RefreshTokenDays { get; }

    public TokenService(IConfiguration configuration)
    {
        _key = Encoding.UTF8.GetBytes(configuration["Auth:SigningKey"] ?? throw new InvalidOperationException("Auth:SigningKey is required."));
        _accessMinutes = int.TryParse(configuration["Auth:AccessTokenMinutes"], out var value) ? value : 15;
        RefreshTokenDays = int.TryParse(configuration["Auth:RefreshTokenDays"], out var refreshDays) ? refreshDays : 30;
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
            if (!isLegacyToken && !long.TryParse(values[2], out deviceId) || !long.TryParse(values[expiresIndex], out var expires) || values[tokenIdIndex].Length != 32) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expires) return false;
            payload = new AccessTokenPayload(userId, tenantId, deviceId, expires, values[tokenIdIndex]);
            return true;
        }
        catch (FormatException) { return false; }
    }

    private string Sign(string text)
    {
        using var hmac = new HMACSHA256(_key);
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }

    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
}

public sealed class SecretProtector
{
    private readonly byte[] _key;
    public SecretProtector(IConfiguration configuration)
    {
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuration["Auth:SigningKey"] ?? throw new InvalidOperationException("Auth:SigningKey is required.")));
    }
    public byte[] Encrypt(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(value);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);
        return result;
    }
}

public sealed record ApiResponse<T>(int Code, string Msg, T? Data)
{
    public static ApiResponse<T> Ok(T value) => new(0, "ok", value);
    public static ApiResponse<T> Fail(int code, string message) => new(code, message, default);
}

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var bytes = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(bytes)}";
    }

    public static bool Verify(string password, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)) return false;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[2]), iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(parts[3]));
    }
}

public static class DbValue
{
    public static string Json<T>(T value) => JsonSerializer.Serialize(value);
    public static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
}
