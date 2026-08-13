using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.AI;

namespace HomeMind.Business.IServices.AI;

/// <summary>AI 配置业务服务约定；配置按用户隔离，密钥只存密文。</summary>
public interface IAiConfigServices
{
    /// <summary>读取当前用户的 AI 配置（含启用开关）。</summary>
    /// <param name="userId">当前用户主键，来自 JWT。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务结果：Data 包含 endpoint/model/temperature/hasApiKey/enabled。</returns>
    Task<ServiceResult> GetAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>保存当前用户的 AI 配置；apiKey 为空表示保留已保存的密钥。</summary>
    /// <param name="userId">当前用户主键，来自 JWT。</param>
    /// <param name="request">AI 配置保存请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保存后配置的业务结果。</returns>
    Task<ServiceResult> SaveAsync(long userId, AiConfigRequest request, CancellationToken cancellationToken = default);

    /// <summary>判断当前用户是否启用了 AI 生成能力。供 <c>/api/v1/ai/{generate,chat,stream}</c> 与专家运行闸门调用。</summary>
    /// <param name="userId">当前用户主键，来自 JWT。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>未配置或已禁用均返回 false，否则返回 true。</returns>
    Task<bool> IsEnabledAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Validate that AI generation has an enabled, complete runtime configuration.</summary>
    Task<ServiceResult> EnsureRuntimeAvailableAsync(long userId, CancellationToken cancellationToken = default);
}
