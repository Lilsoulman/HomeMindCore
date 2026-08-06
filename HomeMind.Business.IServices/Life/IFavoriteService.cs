using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Life;

namespace HomeMind.Business.IServices.Life;

/// <summary>
/// 个人偏好收藏服务抽象。负责收藏 CRUD、可见性过滤（private 仅本人可读写、family 家庭内可读）、
/// 软删除与审计；翻牌与行程的读取由个人生活专家运行经本服务完成，Controller 不直接查询实体。
/// </summary>
public interface IFavoriteService
{
    /// <summary>按分类与可见性列出当前成员可见的收藏。</summary>
    /// <param name="homeId">家庭主键，由 JWT 推导。</param>
    /// <param name="actorUserId">当前操作用户标识。</param>
    /// <param name="category">可选分类过滤：restaurant / travel / material。</param>
    /// <param name="visibility">可选可见性过滤：private / family。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>收藏视图列表；过滤参数非法返回 422。</returns>
    Task<ServiceResult> ListAsync(long homeId, long actorUserId, string? category, string? visibility, CancellationToken cancellationToken = default);

    /// <summary>获取单条当前成员可见的收藏。</summary>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="actorUserId">当前操作用户标识。</param>
    /// <param name="favoriteId">收藏主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>收藏视图；不存在或不可见返回 404。</returns>
    Task<ServiceResult> GetAsync(long homeId, long actorUserId, long favoriteId, CancellationToken cancellationToken = default);

    /// <summary>创建收藏；归属成员默认解析为当前成员，可显式指定同家庭成员。</summary>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="actorUserId">当前操作用户标识。</param>
    /// <param name="request">创建请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建成功返回 201 与收藏视图；校验失败返回 422。</returns>
    Task<ServiceResult> CreateAsync(long homeId, long actorUserId, FavoriteCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>更新收藏；仅归属成员本人或家庭管理员可操作。</summary>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="actorUserId">当前操作用户标识。</param>
    /// <param name="favoriteId">收藏主键。</param>
    /// <param name="request">更新请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新成功返回 200；不存在返回 404；无权限返回 403。</returns>
    Task<ServiceResult> UpdateAsync(long homeId, long actorUserId, long favoriteId, FavoriteUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>软删除收藏并写审计；仅归属成员本人或家庭管理员可操作。</summary>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="actorUserId">当前操作用户标识。</param>
    /// <param name="favoriteId">收藏主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除成功返回 200；不存在返回 404；无权限返回 403。</returns>
    Task<ServiceResult> DeleteAsync(long homeId, long actorUserId, long favoriteId, CancellationToken cancellationToken = default);

    /// <summary>从对话导入收藏；记录来源并写审计。AI 对话提取部分依赖 AI 运行时，按部署环境验证。</summary>
    /// <param name="homeId">家庭主键。</param>
    /// <param name="actorUserId">当前操作用户标识。</param>
    /// <param name="request">导入请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入成功返回 201 与收藏视图；校验失败返回 422。</returns>
    Task<ServiceResult> ImportAsync(long homeId, long actorUserId, FavoriteImportRequest request, CancellationToken cancellationToken = default);
}
