using HomeMind.Business.IServices.Expert;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Expert;

/// <summary>Expert 目录读取服务。Expert 不在此层执行任何 Skill 或 Connector 调用。</summary>
public sealed class ExpertCatalogServices : IExpertCatalogServices
{
    private readonly HomeMindDbContext _db;
    public ExpertCatalogServices(HomeMindDbContext db) => _db = db;

    public async Task<ServiceResult> ListAsync(long userId, long tenantId, string? query, string? category, string? type, string? scope, CancellationToken cancellationToken = default)
    {
        var experts = await (from expert in _db.Experts
                             join version in _db.ExpertVersions on expert.Id equals version.ExpertId
                             where expert.Status == "active" && expert.DeletedAt == null && version.Status == "published" && (expert.TenantId == 1 || expert.TenantId == tenantId)
                                   && (scope == "mine" ? expert.OwnerUserId == userId
                                       : scope == "all" ? (expert.OwnerUserId == null || expert.OwnerUserId == userId)
                                       : expert.OwnerUserId == null)
                                   && (category == null || expert.Category == category)
                                   && (query == null || expert.Name.Contains(query) || expert.Code.Contains(query))
                             select new ExpertCatalogItemView(
                                 expert.Id, "expert", expert.OwnerUserId == null ? "basic" : "mine",
                                 expert.Code, expert.Name, expert.Category, expert.Description, version.EstimatedCredits)).ToListAsync(cancellationToken);
        var groups = await (from expertGroup in _db.ExpertGroups
                            join version in _db.ExpertGroupVersions on expertGroup.Id equals version.GroupId
                            where expertGroup.Status == "active" && version.Status == "published" && (expertGroup.TenantId == 1 || expertGroup.TenantId == tenantId)
                                  && (category == null || expertGroup.Category == category)
                                  && (query == null || expertGroup.Name.Contains(query) || expertGroup.Code.Contains(query))
                            select new ExpertCatalogItemView(
                                expertGroup.Id, "group", "basic",
                                expertGroup.Code, expertGroup.Name, expertGroup.Category, expertGroup.Description, version.EstimatedCredits)).ToListAsync(cancellationToken);
        IReadOnlyList<ExpertCatalogItemView> result = type == "expert" ? experts : type == "group" ? groups : experts.Concat(groups).ToList();
        return new ServiceResult(200, "查询成功。", result);
    }

    public async Task<ServiceResult> GetAsync(long userId, long tenantId, long expertId, string type, CancellationToken cancellationToken = default)
    {
        if (type == "group")
        {
            var group = await (from item in _db.ExpertGroups
                               join version in _db.ExpertGroupVersions on item.Id equals version.GroupId
                               where item.Id == expertId && item.Status == "active" && version.Status == "published" && (item.TenantId == 1 || item.TenantId == tenantId)
                               orderby version.Version descending
                               select new { item.Id, item.Code, item.Name, item.Category, item.Description, VersionId = version.Id, version.Version, version.OrchestrationPolicy, version.OutputSchema, version.EstimatedCredits }).FirstOrDefaultAsync(cancellationToken);
            return group is null ? new ServiceResult(404, "请求的专家团不存在。") : new ServiceResult(200, "查询成功。", group);
        }
        var expert = await (from item in _db.Experts
                            join version in _db.ExpertVersions on item.Id equals version.ExpertId
                            where item.Id == expertId && item.Status == "active" && item.DeletedAt == null && version.Status == "published" && (item.TenantId == 1 || item.TenantId == tenantId)
                                  && (item.OwnerUserId == null || item.OwnerUserId == userId)
                            orderby version.Version descending
                            select new ExpertDetailView(
                                item.Id, item.Code, item.Name, item.Category, item.Description, item.PrivacyScope,
                                item.OwnerUserId == null ? "basic" : "mine",
                                version.Id, version.Version, version.Persona, version.Methodology, version.PromptTemplate,
                                version.ToolPolicy, version.OutputSchema, version.EstimatedCredits)).FirstOrDefaultAsync(cancellationToken);
        return expert is null ? new ServiceResult(404, "请求的专家不存在。") : new ServiceResult(200, "查询成功。", expert);
    }
}
