using HomeMind.Business.IServices.Connector;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Connectors;

/// <summary>
/// 小红书（xhs）个人级 Connector 工具执行服务：搜索/详情/登录状态执行前统一校验连接器归属
/// （当前租户 + personal 作用域 + 本人 owner + auth_status=connected），未授权统一 404；
/// 工具调用经本地 stdio MCP（xhs-mcp），只读操作解析失败降级为空结果，响应不含凭据与 MCP 内部路径。
/// </summary>
public sealed class XhsConnectorServices : IXhsConnectorServices
{
    private const string XhsProviderCode = "xhs";
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly HomeMindDbContext _db;
    private readonly IXhsMcpClient _xhs;

    /// <summary>构造小红书连接器工具执行服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="xhs">小红书 MCP 客户端。</param>
    public XhsConnectorServices(HomeMindDbContext db, IXhsMcpClient xhs)
    {
        _db = db;
        _xhs = xhs;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SearchNotesAsync(long userId, long tenantId, string query, int limit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new ServiceResult(422, "搜索关键词不能为空。");
        var effectiveLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        if (!await IsAuthorizedAsync(userId, tenantId, cancellationToken))
            return new ServiceResult(404, "小红书连接器未授权或不可用。");

        var result = await _xhs.SearchNotesAsync(query.Trim(), effectiveLimit, cancellationToken);
        var views = result.Notes.Select(note => new XhsNoteSummaryView
        {
            NoteId = note.NoteId,
            Title = note.Title,
            CoverUrl = note.CoverUrl,
            AuthorName = note.AuthorName,
            Link = note.Link
        }).ToArray();
        return new ServiceResult(200, "搜索成功。", views);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetNoteDetailAsync(long userId, long tenantId, string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return new ServiceResult(422, "笔记链接不能为空。");
        if (!await IsAuthorizedAsync(userId, tenantId, cancellationToken))
            return new ServiceResult(404, "小红书连接器未授权或不可用。");

        var detail = await _xhs.GetNoteDetailAsync(url.Trim(), cancellationToken);
        return new ServiceResult(200, "查询成功。", new XhsNoteDetailView
        {
            NoteId = detail.NoteId,
            Title = detail.Title,
            Content = detail.Content,
            Images = detail.Images,
            Link = detail.Link
        });
    }

    /// <inheritdoc />
    public async Task<ServiceResult> GetAuthStatusAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedAsync(userId, tenantId, cancellationToken))
            return new ServiceResult(404, "小红书连接器未授权或不可用。");

        var status = await _xhs.GetAuthStatusAsync(cancellationToken);
        return new ServiceResult(200, "查询成功。", new XhsAuthStatusView { LoggedIn = status.LoggedIn, Message = status.Message });
    }

    /// <summary>校验本人小红书连接器已授权：当前租户 + personal 作用域 + 本人 owner + connected 状态。</summary>
    private async Task<bool> IsAuthorizedAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        var connector = await (from item in _db.WorkspaceConnectors
                               join provider in _db.ConnectorProviders on item.ConnectorProviderId equals provider.Id
                               where item.TenantId == tenantId
                                     && item.BindingScope == "personal"
                                     && item.OwnerUserId == userId
                                     && item.DeletedAt == null
                                     && item.Status == "connected"
                                     && item.AuthStatus == WorkspaceConnectorAuthStatus.Connected
                                     && provider.Code == XhsProviderCode
                               select item).FirstOrDefaultAsync(cancellationToken);
        return connector is not null;
    }
}
