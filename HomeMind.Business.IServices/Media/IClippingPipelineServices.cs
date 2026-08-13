using HomeMind.Common.Model.ViewModel.Common;

namespace HomeMind.Business.IServices.Media;

/// <summary>剪辑四引擎流水线的排队与后台处理契约。</summary>
public interface IClippingPipelineServices
{
    /// <summary>将指定剪辑任务置为待处理状态。</summary>
    /// <param name="taskId">剪辑任务主键。</param>
    /// <param name="tenantId">任务所属租户。</param>
    /// <param name="startStage">本次重做的起始引擎阶段。</param>
    /// <param name="allowSeedance">用户是否请求生成式补充片段。</param>
    /// <param name="costConfirmed">用户是否确认生成式补充成本。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>排队结果。</returns>
    Task<ServiceResult> QueueAsync(long taskId, long tenantId, string startStage, bool allowSeedance, bool costConfirmed, CancellationToken cancellationToken = default);

    /// <summary>处理一个已排队的剪辑任务。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实际处理的任务数量。</returns>
    Task<int> ProcessNextAsync(CancellationToken cancellationToken = default);
}

/// <summary>单个剪辑引擎的受控执行契约。</summary>
public interface IClippingEngine
{
    /// <summary>公开阶段标识。</summary>
    string Stage { get; }

    /// <summary>检查部署配置和进程健康状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可执行时为成功；失败消息不得包含命令、路径或凭据。</returns>
    Task<ClippingEngineResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>执行该引擎的受控命令。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果；不得用占位结果表示成功。</returns>
    Task<ClippingEngineResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>剪辑引擎的展示安全执行结果。</summary>
/// <param name="Succeeded">是否成功完成。</param>
/// <param name="Message">可向客户端展示的脱敏说明。</param>
public sealed record ClippingEngineResult(bool Succeeded, string Message);
