using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HomeMind.CreatorMcp;

internal sealed class CreatorCenterClient
{
    private readonly HttpClient _httpClient;

    public CreatorCenterClient(CreatorCenterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            throw new InvalidOperationException("同步需要设置 NEXUSMIND_ACCESS_TOKEN。");
        }

        _httpClient = new HttpClient { BaseAddress = new Uri($"{options.ApiBaseUrl}/") };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
    }

    public async Task<IReadOnlyList<CreatorItem>> FetchAsync(bool includeSensitiveData, CancellationToken cancellationToken)
    {
        var items = new List<CreatorItem>();
        items.AddRange(await FetchCatalogAsync("expert", includeSensitiveData, cancellationToken));
        items.AddRange(await FetchCatalogAsync("group", includeSensitiveData, cancellationToken));
        items.AddRange(await FetchSkillsAsync(includeSensitiveData, cancellationToken));
        return items;
    }

    private async Task<IReadOnlyList<CreatorItem>> FetchCatalogAsync(string type, bool includeSensitiveData, CancellationToken cancellationToken)
    {
        var catalog = await GetDataAsync($"api/v1/experts?type={type}", cancellationToken) as JsonArray
            ?? throw new InvalidOperationException("创作者中心返回的专家目录格式无效。");
        var items = new List<CreatorItem>();
        foreach (var entry in catalog.OfType<JsonObject>())
        {
            var id = entry["Id"]?.GetValue<long>() ?? entry["id"]?.GetValue<long>() ?? 0;
            if (id <= 0) continue;

            var detail = await GetDataAsync($"api/v1/experts/{id}?type={type}", cancellationToken) as JsonObject ?? entry;
            if (!includeSensitiveData)
            {
                detail.Remove("PromptTemplate");
                detail.Remove("promptTemplate");
            }

            items.Add(new CreatorItem(
                type,
                id.ToString(),
                ReadString(entry, "Code", "code"),
                ReadString(entry, "Name", "name") ?? $"{type}-{id}",
                ReadString(entry, "Category", "category"),
                ReadString(entry, "Description", "description"),
                detail.ToJsonString(),
                includeSensitiveData));
        }

        return items;
    }

    private async Task<IReadOnlyList<CreatorItem>> FetchSkillsAsync(bool includeSensitiveData, CancellationToken cancellationToken)
    {
        var skills = await GetDataAsync("api/v1/skills", cancellationToken) as JsonArray
            ?? throw new InvalidOperationException("创作者中心返回的技能目录格式无效。");
        var items = new List<CreatorItem>();
        foreach (var skill in skills.OfType<JsonObject>())
        {
            var id = skill["Id"]?.GetValue<long>() ?? skill["id"]?.GetValue<long>() ?? 0;
            if (id <= 0) continue;
            if (!includeSensitiveData)
            {
                skill.Remove("Prompt");
                skill.Remove("prompt");
            }

            var name = ReadString(skill, "Name", "name") ?? $"skill-{id}";
            items.Add(new CreatorItem("skill", id.ToString(), null, name, "skill", null, skill.ToJsonString(), includeSensitiveData));
        }

        return items;
    }

    private async Task<JsonNode?> GetDataAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"创作者中心请求失败 ({(int)response.StatusCode})：{body}");
        }

        var document = JsonNode.Parse(body) as JsonObject;
        var code = document?["Code"]?.GetValue<int>() ?? document?["code"]?.GetValue<int>();
        if (code is not 0)
        {
            throw new InvalidOperationException(document?["Message"]?.GetValue<string>() ?? document?["message"]?.GetValue<string>() ?? "创作者中心返回业务错误。");
        }

        return document?["Data"] ?? document?["data"];
    }

    private static string? ReadString(JsonObject value, string pascalName, string camelName) =>
        value[pascalName]?.GetValue<string>() ?? value[camelName]?.GetValue<string>();
}
