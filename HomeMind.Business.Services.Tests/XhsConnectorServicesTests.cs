using System;
using System.Linq;
using System.Threading.Tasks;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors;
using HomeMind.Business.Services.Connectors.Mcp;
using HomeMind.Common.Model.Entities.SmartHome;
using HomeMind.Common.Model.ViewModel.Data.Connectors;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>
/// 小红书连接器工具执行服务定向测试：连接器归属与授权校验（未授权 404）、
/// 搜索/详情 Mock 映射、limit 截断、参数校验与登录状态查询。
/// </summary>
public class XhsConnectorServicesTests
{
    /// <summary>已授权连接器搜索返回 Mock 笔记映射，limit 生效。</summary>
    [Fact]
    public async Task Search_With_Authorized_Connector_Returns_Mapped_Notes()
    {
        await using var db = NewDb("xhs-search");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db, new MockXhsMcpClient());

        var result = await services.SearchNotesAsync(userId: 10, tenantId: 1, query: "旅行", limit: 2, default);

        Assert.Equal(200, result.StatusCode);
        var notes = ReadData<XhsNoteSummaryView[]>(result);
        Assert.Equal(2, notes.Length);
        Assert.Equal("旅行·示例笔记1", notes[0].Title);
        Assert.Contains("mock.example.com", notes[0].CoverUrl);
    }

    /// <summary>未授权（无连接器）搜索统一返回 404。</summary>
    [Fact]
    public async Task Search_Without_Authorized_Connector_Returns_404()
    {
        await using var db = NewDb("xhs-search-404");
        var services = CreateServices(db, new MockXhsMcpClient());

        var result = await services.SearchNotesAsync(10, 1, "旅行", 0, default);

        Assert.Equal(404, result.StatusCode);
    }

    /// <summary>连接器存在但已撤销（auth_status=revoked）视为未授权，返回 404。</summary>
    [Fact]
    public async Task Search_With_Revoked_Connector_Returns_404()
    {
        await using var db = NewDb("xhs-search-revoked");
        SeedConnector(db, authStatus: WorkspaceConnectorAuthStatus.Revoked);
        var services = CreateServices(db, new MockXhsMcpClient());

        var result = await services.SearchNotesAsync(10, 1, "旅行", 0, default);

        Assert.Equal(404, result.StatusCode);
    }

    /// <summary>空搜索关键词返回 422。</summary>
    [Fact]
    public async Task Search_With_Empty_Query_Returns_422()
    {
        await using var db = NewDb("xhs-search-422");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db, new MockXhsMcpClient());

        var result = await services.SearchNotesAsync(10, 1, "  ", 0, default);

        Assert.Equal(422, result.StatusCode);
    }

    /// <summary>笔记详情返回脱敏字段映射。</summary>
    [Fact]
    public async Task GetDetail_With_Authorized_Connector_Returns_Mapped_Detail()
    {
        await using var db = NewDb("xhs-detail");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db, new MockXhsMcpClient());

        var result = await services.GetNoteDetailAsync(10, 1, "https://mock.example.com/note/1", default);

        Assert.Equal(200, result.StatusCode);
        var detail = ReadData<XhsNoteDetailView>(result);
        Assert.Equal("示例笔记详情", detail.Title);
        Assert.Single(detail.Images);
    }

    /// <summary>空笔记链接返回 422；未授权返回 404。</summary>
    [Fact]
    public async Task GetDetail_Validates_Input_And_Authorization()
    {
        await using var db = NewDb("xhs-detail-validate");
        var services = CreateServices(db, new MockXhsMcpClient());

        var emptyUrl = await services.GetNoteDetailAsync(10, 1, " ", default);
        Assert.Equal(422, emptyUrl.StatusCode);

        var unauthorized = await services.GetNoteDetailAsync(10, 1, "https://mock.example.com/note/1", default);
        Assert.Equal(404, unauthorized.StatusCode);
    }

    /// <summary>登录状态查询仅在授权连接器下返回。</summary>
    [Fact]
    public async Task AuthStatus_Requires_Authorized_Connector()
    {
        await using var db = NewDb("xhs-auth-status");
        SeedAuthorizedConnector(db);
        var services = CreateServices(db, new MockXhsMcpClient(loggedIn: true));

        var result = await services.GetAuthStatusAsync(10, 1, default);

        Assert.Equal(200, result.StatusCode);
        var view = ReadData<XhsAuthStatusView>(result);
        Assert.True(view.LoggedIn);
    }

    private static XhsConnectorServices CreateServices(HomeMindDbContext db, IXhsMcpClient xhs) => new(db, xhs);

    private static HomeMindDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<HomeMindDbContext>()
            .UseInMemoryDatabase($"hm-b26-xhs-{name}-{Guid.NewGuid()}")
            .Options);

    private static void SeedAuthorizedConnector(HomeMindDbContext db) => SeedConnector(db, authStatus: WorkspaceConnectorAuthStatus.Connected);

    private static void SeedConnector(HomeMindDbContext db, string authStatus)
    {
        db.ConnectorProviders.Add(new ConnectorProvider
        {
            Id = 1,
            Code = "xhs",
            Name = "小红书",
            Provider = "xhs_mcp",
            ConnectorType = "social",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.WorkspaceConnectors.Add(new WorkspaceConnector
        {
            Id = 1,
            TenantId = 1,
            ConnectorProviderId = 1,
            BindingScope = "personal",
            OwnerUserId = 10,
            Name = "小红书",
            CredentialRef = "local://xhs-sessions/1",
            Status = "connected",
            AuthStatus = authStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static T ReadData<T>(HomeMind.Common.Model.ViewModel.Common.ServiceResult result) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(result.Data))!;
}
