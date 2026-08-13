using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HomeMind.Business.Services.Tests;

/// <summary>小红书 MCP 搜索响应解析定向测试。</summary>
public class XhsMcpClientTests
{
    /// <summary>支持 xhs-mcp 的 data.feeds 与 noteCard 嵌套响应结构。</summary>
    [Fact]
    public async Task Search_Parses_Nested_Data_Feeds_Response()
    {
        var process = new FakeMcpProcessClient(JsonNode.Parse("""
        {
          "data": {
            "feeds": [
              {
                  "id": "note-1",
                "xsecToken": "token-1",
                "link": "https://www.xiaohongshu.com/explore/note-1",
                "noteCard": {
                  "displayTitle": "嵌套笔记标题",
                  "user": { "nickName": "嵌套作者" },
                  "cover": { "urlDefault": "https://img.example.com/cover.jpg" }
                }
              }
            ]
          }
        }
        """)!);
        var client = new XhsMcpClient(process, NullLogger<XhsMcpClient>.Instance);

        var result = await client.SearchNotesAsync("旅行", 10, "local://xhs-sessions/1");

        var note = Assert.Single(result.Notes);
        Assert.Equal("note-1", note.NoteId);
        Assert.Equal("嵌套笔记标题", note.Title);
        Assert.Equal("嵌套作者", note.AuthorName);
        Assert.Equal("https://img.example.com/cover.jpg", note.CoverUrl);
        Assert.Equal("https://www.xiaohongshu.com/explore/note-1?xsec_token=token-1&xsec_source=pc_feed", note.Link);
        Assert.Equal("xhs_search_note", process.ToolName);
        Assert.Equal("local://xhs-sessions/1", process.Arguments!["credentialRef"]!.GetValue<string>());
    }

    [Fact]
    public async Task Search_When_Feed_Has_No_Link_Builds_Explore_Link_From_Note_Id()
    {
        var process = new FakeMcpProcessClient(JsonNode.Parse("""
        {
          "feeds": [
            {
              "noteId": "6a6eb6ec000000002403f48f",
              "noteCard": { "displayTitle": "笔记标题" }
            }
          ]
        }
        """)!);
        var client = new XhsMcpClient(process, NullLogger<XhsMcpClient>.Instance);

        var result = await client.SearchNotesAsync("旅行", 10);

        var note = Assert.Single(result.Notes);
        Assert.Equal("https://www.xiaohongshu.com/explore/6a6eb6ec000000002403f48f", note.Link);
    }

    /// <summary>缺少笔记数组的响应视为 MCP 结构异常，不返回成功空数组。</summary>
    [Fact]
    public async Task Search_When_Response_Has_No_Note_Array_Throws()
    {
        var client = new XhsMcpClient(
            new FakeMcpProcessClient(JsonNode.Parse("""{ "data": { "status": "ok" } }""")!),
            NullLogger<XhsMcpClient>.Instance);

        await Assert.ThrowsAsync<McpClientException>(() => client.SearchNotesAsync("旅行", 10));
    }

    [Fact]
    public async Task GetNoteDetail_When_Bridge_Rejects_Incomplete_Shared_Link_Throws_Validation_Exception()
    {
        var client = new XhsMcpClient(
            new FakeMcpProcessClient(JsonNode.Parse("""
            { "success": false, "error": "MissingXsecToken", "message": "该笔记链接缺少访问令牌，请从小红书复制完整分享链接后重试。" }
            """)!),
            NullLogger<XhsMcpClient>.Instance);

        var error = await Assert.ThrowsAsync<XhsNoteDetailException>(() =>
            client.GetNoteDetailAsync("https://www.xiaohongshu.com/explore/6a5b43a7000000001b01caf1"));

        Assert.Equal(422, error.StatusCode);
    }

    [Fact]
    public async Task GetNoteDetail_When_Upstream_Reports_Missing_Note_Throws_NotFound_Exception()
    {
        var client = new XhsMcpClient(
            new FakeMcpProcessClient(JsonNode.Parse("""
            { "success": false, "error": "FeedError", "message": "Feed not found" }
            """)!),
            NullLogger<XhsMcpClient>.Instance);

        var error = await Assert.ThrowsAsync<XhsNoteDetailException>(() =>
            client.GetNoteDetailAsync("https://www.xiaohongshu.com/explore/note"));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("笔记不存在或当前账号无权访问。", error.Message);
    }

    private sealed class FakeMcpProcessClient(JsonNode? response) : IMcpProcessClient
    {
        public string? ToolName { get; private set; }
        public JsonObject? Arguments { get; private set; }

        public Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default)
        {
            ToolName = toolName;
            Arguments = arguments;
            return Task.FromResult(response);
        }

        public Task<JsonObject?> ListToolsAsync(CancellationToken cancellationToken = default) => Task.FromResult<JsonObject?>(null);

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
