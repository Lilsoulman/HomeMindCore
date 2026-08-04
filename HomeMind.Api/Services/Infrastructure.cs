using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Api.Services;

public sealed record UserContext(long UserId, long TenantId, long DeviceId, string Role, long ExpiresAtUnixTime, string TokenId);

public sealed class SecretProtector
{
    private readonly byte[] _key;
    public SecretProtector(IConfiguration configuration)
    {
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuration["Auth:SigningKey"] ?? throw new InvalidOperationException("缺少认证签名密钥配置。")));
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
    public static ApiResponse<T> Ok(T value) => new(0, "操作成功", value);
    public static ApiResponse<T> Fail(int code, string message) => new(code, message, default);
}
