using HomeMind.Business.Services.Media;
using HomeMind.Business.IServices.AI;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HomeMind.Common.Model.ViewModel.Data.Media;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// 剪辑对话引导定向测试（B32）：规则意图匹配、无状态 context 校验推进
/// （collecting_materials → generating_plan → reviewing → done）、
/// 非法步进与空消息 422、模板回复与 suggestions。
/// </summary>
public class ClippingChatServicesTests
{
    private readonly ClippingChatServices _services;

    public ClippingChatServicesTests()
    {
        var db = new HomeMindDbContext(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b32-{Guid.NewGuid()}").Options);
        _services = new ClippingChatServices(db);
    }

    /// <summary>剪辑意图消息进入引导：无素材时提示上传或填写路径。</summary>
    [Fact]
    public async Task Chat_Intent_WithoutMaterials_Guides_To_Upload()
    {
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("帮我剪一下探店视频", null), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Contains("上传素材", response.Reply);
        Assert.Contains("上传素材", response.Suggestions);
        Assert.Equal("collecting_materials", response.Context.Step);
    }

    /// <summary>无剪辑意图消息返回友好引导，不推进步骤。</summary>
    [Fact]
    public async Task Chat_NoIntent_Returns_Friendly_Reply()
    {
        var context = new ClippingChatContext("collecting_materials", null, null, null);
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("今天天气怎么样", context), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Contains("快速剪辑", response.Reply);
        Assert.Equal("collecting_materials", response.Context.Step);
    }

    /// <summary>素材就绪后推进到生成目标步骤，提示创作目标。</summary>
    [Fact]
    public async Task Chat_WithMaterials_Advances_To_Goal()
    {
        var context = new ClippingChatContext("collecting_materials", new[] { "/data/materials/a.mp4" }, null, null);
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("帮我剪视频", context), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Contains("创作目标", response.Reply);
        Assert.Equal("generating_plan", response.Context.Step);
    }

    /// <summary>目标齐备后建议生成方案，进入 generating_plan。</summary>
    [Fact]
    public async Task Chat_WithGoal_Suggests_GeneratePlan()
    {
        var context = new ClippingChatContext("collecting_materials", new[] { "/data/materials/a.mp4" }, "竖屏 30 秒", null);
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("帮我剪视频", context), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Contains("生成方案", response.Suggestions);
        Assert.Equal("generating_plan", response.Context.Step);
        Assert.Equal("竖屏 30 秒", response.Context.Goal);
    }

    /// <summary>generating_plan 步骤用户直接给出目标文本，被记录为目标。</summary>
    [Fact]
    public async Task Chat_GeneratingPlan_MessageBecomesGoal()
    {
        var context = new ClippingChatContext("generating_plan", new[] { "/data/materials/a.mp4" }, null, null);
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("竖屏 45 秒，加字幕", context), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Contains("竖屏 45 秒，加字幕", response.Context.Goal);
        Assert.Contains("生成方案", response.Suggestions);
    }

    /// <summary>reviewing 步骤提供确认/重新生成建议。</summary>
    [Fact]
    public async Task Chat_Reviewing_Suggests_Confirm_And_Revise()
    {
        var context = new ClippingChatContext("reviewing", new[] { "/data/materials/a.mp4" }, "竖屏 30 秒", true);
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("这个方案可以", context), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Contains("确认方案", response.Suggestions);
        Assert.Contains("修改目标重新生成", response.Suggestions);
    }

    /// <summary>done 步骤返回重新剪辑引导并重置上下文。</summary>
    [Fact]
    public async Task Chat_Done_Resets_Context()
    {
        var context = new ClippingChatContext("done", new[] { "/data/materials/a.mp4" }, "竖屏 30 秒", true);
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("再来一次", context), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Equal("collecting_materials", response.Context.Step);
        Assert.Null(response.Context.Materials);
        Assert.Null(response.Context.Goal);
    }

    /// <summary>空消息返回 422。</summary>
    [Fact]
    public async Task Chat_EmptyMessage_Returns422()
    {
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("  ", null), default);

        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>非法步骤上下文返回 422。</summary>
    [Fact]
    public async Task Chat_InvalidStep_Returns422()
    {
        var result = await _services.ChatAsync(10, 1, new ClippingChatRequest("帮我剪视频", new ClippingChatContext("unknown_step", null, null, null)), default);

        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>V2.8：首次对话创建持久化任务，后续携带 taskId 可恢复同一任务。</summary>
    [Fact]
    public async Task Chat_Creates_And_Resumes_Persisted_Task()
    {
        var first = await _services.ChatAsync(10, 1, new ClippingChatRequest("帮我剪视频", null), default);
        var firstResponse = Assert.IsType<ClippingChatResponse>(first.Data);
        Assert.True(firstResponse.TaskId > 0);

        var resumed = await _services.ChatAsync(10, 1, new ClippingChatRequest("继续", firstResponse.Context, firstResponse.TaskId), default);
        Assert.Equal(200, resumed.StatusCode);
        Assert.Equal(firstResponse.TaskId, Assert.IsType<ClippingChatResponse>(resumed.Data).TaskId);
    }

    /// <summary>B39：AI 启用时将一句话解析为受限参数、写入任务目标并返回确认卡。</summary>
    [Fact]
    public async Task Chat_AiEnabled_Parses_Goal_Persists_Parameters_And_Returns_Confirmation()
    {
        var db = NewDb("ai-success");
        var protector = new SecretProtector(BuildConfig());
        await EnableAiAsync(db, protector);
        var services = new ClippingChatServices(db, new FakeLlm("""{"target_duration":30,"aspect_ratio":"9:16","style":"快节奏","subtitle":true,"mood":"活力"}"""), protector);

        var result = await services.ChatAsync(10, 1, new ClippingChatRequest("剪成 30 秒竖屏快节奏带字幕", new ClippingChatContext("collecting_materials", new[] { "/data/a.mp4" }, null, null)), default);

        Assert.Equal(200, result.StatusCode);
        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Equal("generating_plan", response.Context.Step);
        Assert.Contains("30 秒", response.Context.Goal);
        Assert.NotNull(response.Confirmation);
        Assert.Equal("已理解", response.Confirmation!.Title);
        var task = await db.ClippingTasks.SingleAsync();
        using var goal = System.Text.Json.JsonDocument.Parse(task.Goal!);
        Assert.Equal(30, goal.RootElement.GetProperty("target_duration").GetInt32());
        Assert.True(goal.RootElement.GetProperty("subtitle").GetBoolean());
    }

    /// <summary>B39：模型成功返回但参数不符合 schema 时明确拒绝为 422。</summary>
    [Fact]
    public async Task Chat_AiReturns_InvalidParameters_Returns422()
    {
        var db = NewDb("ai-invalid");
        var protector = new SecretProtector(BuildConfig());
        await EnableAiAsync(db, protector);
        var services = new ClippingChatServices(db, new FakeLlm("""{"target_duration":30,"aspect_ratio":"9:16","style":"快节奏","subtitle":true,"mood":"活力","unexpected":true}"""), protector);

        var result = await services.ChatAsync(10, 1, new ClippingChatRequest("剪短一点", new ClippingChatContext("collecting_materials", new[] { "/data/a.mp4" }, null, null)), default);

        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>B39：AI 被用户关闭时不调用模型并保持既有模板问卷。</summary>
    [Fact]
    public async Task Chat_AiDisabled_Falls_Back_To_Template_Guide()
    {
        var db = NewDb("ai-disabled");
        var protector = new SecretProtector(BuildConfig());
        await EnableAiAsync(db, protector, false);
        var services = new ClippingChatServices(db, new ThrowingLlm(), protector);

        var result = await services.ChatAsync(10, 1, new ClippingChatRequest("剪成 30 秒竖屏", new ClippingChatContext("collecting_materials", new[] { "/data/a.mp4" }, null, null)), default);

        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(response.Confirmation);
        Assert.Equal("generating_plan", response.Context.Step);
    }

    /// <summary>B39：模型超时或调用失败时自动回退模板问卷，不向客户端暴露内部错误。</summary>
    [Fact]
    public async Task Chat_AiTimeout_Falls_Back_To_Template_Guide()
    {
        var db = NewDb("ai-timeout");
        var protector = new SecretProtector(BuildConfig());
        await EnableAiAsync(db, protector);
        var services = new ClippingChatServices(db, new FakeLlm(null, false, LlmErrorCodes.Timeout), protector);

        var result = await services.ChatAsync(10, 1, new ClippingChatRequest("剪成 30 秒竖屏", new ClippingChatContext("collecting_materials", new[] { "/data/a.mp4" }, null, null)), default);

        var response = Assert.IsType<ClippingChatResponse>(result.Data);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(response.Confirmation);
    }

    /// <summary>创建隔离的 InMemory 数据库。</summary>
    private static HomeMindDbContext NewDb(string name) => new(new DbContextOptionsBuilder<HomeMindDbContext>().UseInMemoryDatabase($"hm-b39-{name}-{Guid.NewGuid()}").Options);

    /// <summary>写入测试所需的用户级 AI 配置。</summary>
    private static async Task EnableAiAsync(HomeMindDbContext db, SecretProtector protector, bool enabled = true)
    {
        db.AiConfigs.Add(new AiConfig
        {
            UserId = 10,
            Endpoint = "https://example.invalid/v1",
            Model = "test-model",
            Temperature = 0,
            Enabled = enabled,
            ApiKeyEncrypted = protector.Encrypt("test-key")
        });
        await db.SaveChangesAsync();
    }

    /// <summary>构造密钥加解密所需的稳定测试配置。</summary>
    private static IConfiguration BuildConfig() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Auth:SigningKey"] = "b39-test-signing-key-must-have-at-least-32-bytes"
    }).Build();

    /// <summary>返回预设模型结果的轻量 LLM 替身。</summary>
    private sealed class FakeLlm : ILLMClient
    {
        private readonly string? _content;
        private readonly bool _success;
        private readonly string? _errorCode;

        public FakeLlm(string? content, bool success = true, string? errorCode = null)
        {
            _content = content;
            _success = success;
            _errorCode = errorCode;
        }

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmCompletion(_content ?? string.Empty, null, _success, _errorCode, _success ? null : "模型调用失败"));
    }

    /// <summary>验证禁用 AI 时绝不会调用模型。</summary>
    private sealed class ThrowingLlm : ILLMClient
    {
        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("不应调用模型。");
    }
}
