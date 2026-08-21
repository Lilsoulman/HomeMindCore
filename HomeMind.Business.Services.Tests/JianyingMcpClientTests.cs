using System.Text.Json;
using System.Text.Json.Nodes;
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.Services.Connectors.Mcp;
using Xunit;

namespace HomeMind.Business.Services.Tests;

public class JianyingMcpClientTests
{
    [Fact]
    public async Task GenerateDraft_MapsBeatEdlToVideoAndMusicTracks()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"homemind-jianying-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "draft_content.json"), "{\"draft\":true}");
        var process = new FakeMcpProcessClient(outputDirectory);
        var client = new JianyingMcpClient(process);

        var content = await client.GenerateDraftAsync("""
            {
              "media_location":"first.mp4",
              "segments":[
                {"media_location":"first.mp4","start":2,"timeline_start":0,"duration":3},
                {"media_location":"second.mp4","start":4,"timeline_start":3,"duration":2}
              ],
              "audio":{"music_location":"music.mp3","source_start":10,"duration":5,"volume":0.8},
              "total_duration":5
            }
            """);

        using var result = JsonDocument.Parse(content);
        Assert.True(result.RootElement.GetProperty("draft").GetBoolean());
        Assert.Equal(2, process.Calls.Count(call => call.ToolName == "add_video_segment"));
        Assert.Equal(1, process.Calls.Count(call => call.ToolName == "add_audio_segment"));

        var firstVideo = process.Calls.First(call => call.ToolName == "add_video_segment");
        Assert.Equal("0s-3s", firstVideo.Arguments!["target_start_end"]!.GetValue<string>());
        Assert.Equal("2s-5s", firstVideo.Arguments!["source_start_end"]!.GetValue<string>());
        var secondVideo = process.Calls.Last(call => call.ToolName == "add_video_segment");
        Assert.Equal("3s-5s", secondVideo.Arguments!["target_start_end"]!.GetValue<string>());
        Assert.Equal("4s-6s", secondVideo.Arguments!["source_start_end"]!.GetValue<string>());
        var audio = process.Calls.Single(call => call.ToolName == "add_audio_segment");
        Assert.Equal("0s-5s", audio.Arguments!["target_start_end"]!.GetValue<string>());
        Assert.Equal("10s-15s", audio.Arguments!["source_start_end"]!.GetValue<string>());
    }

    private sealed class FakeMcpProcessClient(string outputDirectory) : IMcpProcessClient
    {
        public List<(string ToolName, JsonObject? Arguments)> Calls { get; } = [];

        public Task<JsonNode?> CallToolAsync(string toolName, JsonObject? arguments, CancellationToken cancellationToken = default)
        {
            Calls.Add((toolName, arguments));
            JsonNode response = toolName switch
            {
                "create_draft" => new JsonObject { ["draft_id"] = "draft-1" },
                "create_track" => new JsonObject { ["success"] = true, ["data"] = new JsonObject { ["track_id"] = $"track-{Calls.Count}" } },
                "parse_media_info" => new JsonObject { ["success"] = true, ["data"] = new JsonObject { ["duration"] = 60 } },
                "export_draft" => new JsonObject { ["success"] = true, ["data"] = new JsonObject { ["output_path"] = outputDirectory } },
                _ => new JsonObject { ["success"] = true, ["data"] = new JsonObject() }
            };
            return Task.FromResult<JsonNode?>(response);
        }

        public Task<JsonObject?> ListToolsAsync(CancellationToken cancellationToken = default) => Task.FromResult<JsonObject?>(null);
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
