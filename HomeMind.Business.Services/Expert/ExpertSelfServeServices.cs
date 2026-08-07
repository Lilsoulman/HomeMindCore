using System.Text.Json;
using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using ExpertEntity = HomeMind.Common.Model.Entities.Expert;

namespace HomeMind.Business.Services.Expert;

/// <summary>
/// 用户自建专家服务实现：仅本人可创建/更新/软删除（owner_user_id=本人）。
/// 创建自动生成 <c>custom-</c> 前缀编码与 v1 已发布版本；更新生成 version+1 已发布版本（尊重版本不可变不变量）；
/// 删除为软删除，已删专家从目录、运行解析与会话发送全部消失。不写家庭审计（设计 §13.1 仅要求会话审计）。
/// </summary>
public sealed class ExpertSelfServeServices : IExpertSelfServeServices
{
    private const string DefaultMethodology = "从通用方法论出发，先分析、再行动，最后给出可执行的建议。";

    private readonly HomeMindDbContext _db;

    /// <summary>构造自建专家服务。</summary>
    /// <param name="db">数据库上下文。</param>
    public ExpertSelfServeServices(HomeMindDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, ExpertCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category)
            || string.IsNullOrWhiteSpace(request.Persona) || string.IsNullOrWhiteSpace(request.PromptTemplate))
            return new ServiceResult(422, "名称、分类、人设与提示词模板为必填项。");
        if (!string.IsNullOrWhiteSpace(request.ToolPolicyJson) && !IsValidJson(request.ToolPolicyJson))
            return new ServiceResult(422, "工具策略必须是合法 JSON。");

        var code = await GenerateUniqueCodeAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var expert = new ExpertEntity
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            Code = code,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            ExpertType = "custom",
            Status = "active",
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Experts.Add(expert);
        await _db.SaveChangesAsync(cancellationToken);

        _db.ExpertVersions.Add(new ExpertVersion
        {
            TenantId = tenantId,
            ExpertId = expert.Id,
            Version = 1,
            Status = "published",
            Persona = request.Persona.Trim(),
            Methodology = string.IsNullOrWhiteSpace(request.Methodology) ? DefaultMethodology : request.Methodology.Trim(),
            PromptTemplate = request.PromptTemplate.Trim(),
            ToolPolicy = request.ToolPolicyJson,
            EstimatedCredits = request.EstimatedCredits ?? 1
        });
        await _db.SaveChangesAsync(cancellationToken);

        var view = await BuildDetailViewAsync(userId, tenantId, expert.Id, cancellationToken);
        return new ServiceResult(201, "自建专家已创建。", view);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(long userId, long tenantId, long expertId, ExpertUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var expert = await _db.Experts.SingleOrDefaultAsync(
            x => x.Id == expertId && x.TenantId == tenantId && x.OwnerUserId == userId && x.DeletedAt == null, cancellationToken);
        if (expert is null) return new ServiceResult(404, "请求的自建专家不存在。");
        if (expert.RowVersion != request.RowVersion)
            return new ServiceResult(409, "专家已被其他操作修改，请刷新后重试。", null, ApiErrorCodes.OptimisticLockConflict);
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category)
            || string.IsNullOrWhiteSpace(request.Persona) || string.IsNullOrWhiteSpace(request.PromptTemplate))
            return new ServiceResult(422, "名称、分类、人设与提示词模板为必填项。");
        if (!string.IsNullOrWhiteSpace(request.ToolPolicyJson) && !IsValidJson(request.ToolPolicyJson))
            return new ServiceResult(422, "工具策略必须是合法 JSON。");

        var nextVersion = await _db.ExpertVersions.Where(v => v.ExpertId == expert.Id).MaxAsync(v => (int?)v.Version, cancellationToken) ?? 0;
        expert.Name = request.Name.Trim();
        expert.Category = request.Category.Trim();
        expert.Description = request.Description?.Trim();
        expert.RowVersion += 1;
        expert.UpdatedAt = DateTime.UtcNow;
        _db.ExpertVersions.Add(new ExpertVersion
        {
            TenantId = tenantId,
            ExpertId = expert.Id,
            Version = nextVersion + 1,
            Status = "published",
            Persona = request.Persona.Trim(),
            Methodology = string.IsNullOrWhiteSpace(request.Methodology) ? DefaultMethodology : request.Methodology.Trim(),
            PromptTemplate = request.PromptTemplate.Trim(),
            ToolPolicy = request.ToolPolicyJson,
            EstimatedCredits = request.EstimatedCredits ?? 1
        });
        await _db.SaveChangesAsync(cancellationToken);

        var view = await BuildDetailViewAsync(userId, tenantId, expert.Id, cancellationToken);
        return new ServiceResult(200, "自建专家已更新。", view);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long userId, long tenantId, long expertId, CancellationToken cancellationToken = default)
    {
        var expert = await _db.Experts.SingleOrDefaultAsync(
            x => x.Id == expertId && x.TenantId == tenantId && x.OwnerUserId == userId && x.DeletedAt == null, cancellationToken);
        if (expert is null) return new ServiceResult(404, "请求的自建专家不存在。");

        expert.DeletedAt = DateTime.UtcNow;
        expert.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(200, "自建专家已删除。");
    }

    /// <summary>生成租户内唯一的 <c>custom-</c> 前缀编码；撞唯一键时重生成一次。</summary>
    private async Task<string> GenerateUniqueCodeAsync(long tenantId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var code = "custom-" + Guid.NewGuid().ToString("N")[..8];
            if (!await _db.Experts.AnyAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken))
                return code;
        }
        return "custom-" + Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>构造当前用户可见的详情视图（取最新已发布版本）。</summary>
    private async Task<ExpertDetailView?> BuildDetailViewAsync(long userId, long tenantId, long expertId, CancellationToken cancellationToken)
    {
        return await (from item in _db.Experts
                      join version in _db.ExpertVersions on item.Id equals version.ExpertId
                      where item.Id == expertId && item.Status == "active" && item.DeletedAt == null && version.Status == "published"
                            && (item.TenantId == 1 || item.TenantId == tenantId)
                            && (item.OwnerUserId == null || item.OwnerUserId == userId)
                      orderby version.Version descending
                      select new ExpertDetailView(
                          item.Id, item.Code, item.Name, item.Category, item.Description, item.PrivacyScope,
                          item.OwnerUserId == null ? "basic" : "mine",
                          version.Id, version.Version, version.Persona, version.Methodology, version.PromptTemplate,
                          version.ToolPolicy, version.OutputSchema, version.EstimatedCredits)).FirstOrDefaultAsync(cancellationToken);
    }

    private static bool IsValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        try { JsonDocument.Parse(value); return true; } catch (JsonException) { return false; }
    }
}
