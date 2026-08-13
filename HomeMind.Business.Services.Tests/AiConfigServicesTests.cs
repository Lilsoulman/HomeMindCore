using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.Services.AI;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>AI 配置服务定向测试：默认启用、显式禁用、保存幂等（仅 apiKey 提交时覆写密文）。</summary>
public class AiConfigServicesTests
{
    /// <summary>未配置任何 AI 服务时，<c>IsEnabledAsync</c> 应返回 false，避免未保存配置的用户绕过开关。</summary>
    [Fact]
    public async Task IsEnabled_Returns_False_When_No_Config()
    {
        await using var db = NewDb("ai-config-empty");
        var protector = new SecretProtector(BuildConfig());
        var services = new AiConfigServices(db, protector);

        var ok = await services.IsEnabledAsync(userId: 7);

        Assert.False(ok);
    }

    /// <summary>已配置且 <c>Enabled=true</c> 时，<c>IsEnabledAsync</c> 应返回 true。</summary>
    [Fact]
    public async Task IsEnabled_Returns_True_When_Default_Enabled()
    {
        await using var db = NewDb("ai-config-default-enabled");
        var protector = new SecretProtector(BuildConfig());
        db.AiConfigs.Add(new AiConfig
        {
            UserId = 7,
            Endpoint = "https://api.example.com/v1",
            Model = "gpt-4.1-mini",
            Temperature = 0.7,
            ApiKeyEncrypted = protector.Encrypt("sk-test"),
            Enabled = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = new AiConfigServices(db, protector);

        var ok = await services.IsEnabledAsync(userId: 7);

        Assert.True(ok);
    }

    /// <summary>显式将 <c>Enabled=false</c> 持久化后，<c>IsEnabledAsync</c> 必须返回 false。</summary>
    [Fact]
    public async Task IsEnabled_Returns_False_When_Explicitly_Disabled()
    {
        await using var db = NewDb("ai-config-disabled");
        var protector = new SecretProtector(BuildConfig());
        db.AiConfigs.Add(new AiConfig
        {
            UserId = 7,
            Endpoint = "https://api.example.com/v1",
            Model = "gpt-4.1-mini",
            Temperature = 0.7,
            ApiKeyEncrypted = protector.Encrypt("sk-test"),
            Enabled = false,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = new AiConfigServices(db, protector);

        var ok = await services.IsEnabledAsync(userId: 7);

        Assert.False(ok);
    }

    [Fact]
    public async Task EnsureRuntimeAvailable_Returns_Readable_422_For_Missing_Or_Disabled_Config()
    {
        await using var db = NewDb("ai-config-runtime-availability");
        var protector = new SecretProtector(BuildConfig());
        var services = new AiConfigServices(db, protector);

        var missing = await services.EnsureRuntimeAvailableAsync(7);
        Assert.Equal(422, missing.StatusCode);
        Assert.Equal(ApiErrorCodes.PreconditionFailed, missing.Code);
        Assert.Contains("not configured", missing.Message, StringComparison.OrdinalIgnoreCase);

        db.AiConfigs.Add(new AiConfig
        {
            UserId = 7,
            Endpoint = "https://api.example.com/v1",
            Model = "gpt-4.1-mini",
            Temperature = 0.7,
            ApiKeyEncrypted = protector.Encrypt("sk-test"),
            Enabled = false,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var disabled = await services.EnsureRuntimeAvailableAsync(7);
        Assert.Equal(422, disabled.StatusCode);
        Assert.Equal(ApiErrorCodes.PreconditionFailed, disabled.Code);
        Assert.Contains("disabled", disabled.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>仅传 enabled 切换开关时不得清空已保存的 API Key 密文。</summary>
    [Fact]
    public async Task Save_Without_ApiKey_Preserves_Existing_Secret()
    {
        await using var db = NewDb("ai-config-save-no-key");
        var protector = new SecretProtector(BuildConfig());
        var original = protector.Encrypt("sk-original");
        db.AiConfigs.Add(new AiConfig
        {
            UserId = 7,
            Endpoint = "https://api.example.com/v1",
            Model = "gpt-4.1-mini",
            Temperature = 0.7,
            ApiKeyEncrypted = original,
            Enabled = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = new AiConfigServices(db, protector);

        var result = await services.SaveAsync(7,
            new AiConfigRequest("https://api.example.com/v1", "gpt-4.1-mini", 0.5, Enabled: false, ApiKey: null),
            default);

        Assert.True(result.Succeeded);
        var stored = await db.AiConfigs.SingleAsync();
        Assert.False(stored.Enabled);
        Assert.Equal(original, stored.ApiKeyEncrypted);
    }

    /// <summary>空字符串 apiKey 也不得覆写已有密文，避免前端误传空串清空密钥。</summary>
    [Fact]
    public async Task Save_With_Empty_ApiKey_Preserves_Existing_Secret()
    {
        await using var db = NewDb("ai-config-save-empty-key");
        var protector = new SecretProtector(BuildConfig());
        var original = protector.Encrypt("sk-original");
        db.AiConfigs.Add(new AiConfig
        {
            UserId = 7,
            Endpoint = "https://api.example.com/v1",
            Model = "gpt-4.1-mini",
            Temperature = 0.7,
            ApiKeyEncrypted = original,
            Enabled = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var services = new AiConfigServices(db, protector);

        var result = await services.SaveAsync(7,
            new AiConfigRequest("https://api.example.com/v1", "gpt-4.1-mini", 0.5, Enabled: true, ApiKey: ""),
            default);

        Assert.True(result.Succeeded);
        var stored = await db.AiConfigs.SingleAsync();
        Assert.Equal(original, stored.ApiKeyEncrypted);
    }

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b18-aiconfig-{name}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>为 SecretProtector 提供稳定的 32 字节以上签名密钥，避免生产配置依赖。</summary>
    private static Microsoft.Extensions.Configuration.IConfiguration BuildConfig() =>
        new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SigningKey"] = "unit-test-signing-key-32bytes-minimum-aaaa"
            } as IEnumerable<KeyValuePair<string, string?>>)
            .Build();
}
