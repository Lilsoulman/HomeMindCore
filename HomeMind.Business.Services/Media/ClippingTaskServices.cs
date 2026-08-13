using System.Text.Json;
using HomeMind.Business.IServices.Media;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Media;

/// <summary>V2.8 剪辑任务查询实现；只输出 Web P5 所需的展示安全状态。</summary>
public sealed class ClippingTaskServices : IClippingTaskServices
{
    private readonly HomeMindDbContext _db;
    public ClippingTaskServices(HomeMindDbContext db) => _db = db;
    public async Task<ServiceResult> GetAsync(long userId, long tenantId, long taskId, CancellationToken cancellationToken = default)
    {
        var task = await _db.ClippingTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.TenantId == tenantId && x.CreatedByUserId == userId && x.DeletedAt == null, cancellationToken);
        return task is null ? new ServiceResult(404, "请求的剪辑任务不存在。") : new ServiceResult(200, "查询成功。", ToView(task));
    }
    public static ClippingTaskView ToView(ClippingTask task)
    {
        var materials = JsonSerializer.Deserialize<string[]>(task.Materials) ?? [];
        var versions = JsonSerializer.Deserialize<List<ClippingTaskVersionView>>(task.VersionHistory) ?? [];
        object? plan = null;
        if (!string.IsNullOrWhiteSpace(task.CurrentPlan)) using (var doc = JsonDocument.Parse(task.CurrentPlan)) plan = doc.RootElement.Clone();
        return new ClippingTaskView(task.Id, task.RunId, task.Status, task.EngineStage, materials, task.Goal, plan, versions, task.CreatedAt, task.UpdatedAt);
    }
}
