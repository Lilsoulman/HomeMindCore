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

    /// <summary>解密 Encrypt 产生的密文：前 12 字节 nonce、16 字节 tag、其余为密文。</summary>
    /// <param name="encrypted">Encrypt 产生的密文字节数组。</param>
    /// <returns>解密后的明文字符串。</returns>
    /// <exception cref="ArgumentException">密文长度不足以容纳 nonce 与 tag。</exception>
    /// <exception cref="CryptographicException">认证失败，密文已被篡改或密钥不匹配。</exception>
    public string Decrypt(byte[] encrypted)
    {
        if (encrypted.Length < 12 + 16) throw new ArgumentException("密文长度不足，无法解密。", nameof(encrypted));
        var nonce = new byte[12]; var tag = new byte[16]; var cipher = new byte[encrypted.Length - nonce.Length - tag.Length];
        Buffer.BlockCopy(encrypted, 0, nonce, 0, nonce.Length); Buffer.BlockCopy(encrypted, nonce.Length, tag, 0, tag.Length); Buffer.BlockCopy(encrypted, nonce.Length + tag.Length, cipher, 0, cipher.Length);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16); aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
