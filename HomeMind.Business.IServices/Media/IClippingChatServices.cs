using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Media;

namespace HomeMind.Business.IServices.Media;

/// <summary>
/// 剪辑对话引导服务契约（B32）：无状态 context 随请求回传并校验推进
/// （collecting_materials → generating_plan → reviewing → done，非法步进 422），
/// 规则式意图匹配（剪辑关键词）与模板回复 + suggestions 引导按钮。
/// 只引导不执行——方案生成/确认/下载仍走既有 Skill Run 链路；不落库、不新建会话表。
/// </summary>
public interface IClippingChatServices
{
    /// <summary>
    /// 处理一条剪辑对话消息：校验上下文步骤合法性，按步骤推进并返回模板回复与建议操作；
    /// 消息不含剪辑意图时返回友好引导回复（200）。
    /// </summary>
    /// <param name="userId">发起用户标识，由 JWT 推导（当前仅用于鉴权，不落库）。</param>
    /// <param name="tenantId">当前租户标识，由 JWT 推导。</param>
    /// <param name="request">对话请求，含消息与回传上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>引导响应统一响应；上下文步骤非法或消息为空返回 422。</returns>
    Task<ServiceResult> ChatAsync(long userId, long tenantId, ClippingChatRequest request, CancellationToken cancellationToken = default);
}
