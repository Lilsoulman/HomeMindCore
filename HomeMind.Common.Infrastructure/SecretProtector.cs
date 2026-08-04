using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HomeMind.Common.Infrastructure;

/// <summary>使用认证密钥派生的 AES-GCM 字段加密器。</summary>
public sealed class SecretProtector
{
    private readonly byte[] _key;
    public SecretProtector(IConfiguration configuration) => _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuration["Auth:SigningKey"] ?? throw new InvalidOperationException("缺少认证签名密钥配置。")));
    public byte[] Encrypt(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var plain = Encoding.UTF8.GetBytes(value); var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(_key, 16); aes.Encrypt(nonce, plain, cipher, tag);
        var result = new byte[nonce.Length + tag.Length + cipher.Length]; Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length); Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length); Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length); return result;
    }
}
