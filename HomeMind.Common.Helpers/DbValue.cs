using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HomeMind.Common.Helpers;

/// <summary>数据库字段使用的安全值转换工具。</summary>
public static class DbValue
{
    public static string Json<T>(T value) => JsonSerializer.Serialize(value);
    public static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    public static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
