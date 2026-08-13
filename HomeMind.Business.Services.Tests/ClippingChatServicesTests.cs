using HomeMind.Business.Services.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
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
}
