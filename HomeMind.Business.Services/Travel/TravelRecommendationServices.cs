using System.Text.Json;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.IServices.Travel;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Life;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Family;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Travel;

/// <summary>出行推荐实现：偏好与计数存于 family_knowledge（travel 档），景点来自本地 attractions 库。</summary>
public sealed class TravelRecommendationServices : ITravelRecommendationServices
{
    private const string TravelCategory = "travel";
    private const string InterestsKey = "interests";
    private const string ExcludedKey = "excludedAttractionIds";
    private const string CountsKey = "recommendCounts";
    private const int TopCount = 3;

    private readonly HomeMindDbContext _db;
    private readonly IFamilyKnowledgeServices _knowledge;

    public TravelRecommendationServices(HomeMindDbContext db, IFamilyKnowledgeServices knowledge)
    {
        _db = db;
        _knowledge = knowledge;
    }

    public async Task<ServiceResult> GetRecommendationsAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var preferences = await LoadPreferencesAsync(tenantId, cancellationToken);
        var candidates = await _db.TravelAttractions.Where(x => x.IsActive).ToListAsync(cancellationToken);
        var filtered = candidates.Where(x => !preferences.Excluded.Contains(x.Id)).ToArray();
        if (preferences.Interests.Count > 0)
        {
            filtered = filtered.Where(x => MatchesInterests(x, preferences.Interests)).ToArray();
        }
        var ranked = filtered
            .OrderBy(x => preferences.Counts.GetValueOrDefault(x.Id))
            .ThenBy(x => x.Id)
            .Take(TopCount)
            .ToArray();

        foreach (var item in ranked)
        {
            preferences.Counts[item.Id] = preferences.Counts.GetValueOrDefault(item.Id) + 1;
        }
        await SaveCountsAsync(userId, tenantId, preferences.Counts, cancellationToken);

        var items = ranked.Select(x => new
        {
            x.Id,
            x.Name,
            x.City,
            x.Category,
            DurationHours = x.DurationHours,
            CostLevel = x.CostLevel,
            x.WeatherTag,
            Tags = ReadTags(x),
            x.Description,
            Reason = BuildReason(x)
        }).ToArray();
        return new ServiceResult(200, "推荐生成成功。", items);
    }

    public async Task<ServiceResult> SubmitFeedbackAsync(long userId, long tenantId, long attractionId, string choice, CancellationToken cancellationToken = default)
    {
        if (choice is not ("selected" or "alternative" or "not_interested"))
            return new ServiceResult(422, "反馈仅支持 selected、alternative 或 not_interested。");
        var attraction = await _db.TravelAttractions.FirstOrDefaultAsync(x => x.Id == attractionId && x.IsActive, cancellationToken);
        if (attraction is null) return new ServiceResult(404, "请求的景点不存在。");

        var preferences = await LoadPreferencesAsync(tenantId, cancellationToken);
        switch (choice)
        {
            case "selected":
                preferences.Counts[attractionId] = preferences.Counts.GetValueOrDefault(attractionId) + 2;
                break;
            case "alternative":
                preferences.Counts[attractionId] = preferences.Counts.GetValueOrDefault(attractionId) + 1;
                break;
            case "not_interested":
                preferences.Excluded.Add(attractionId);
                preferences.Counts[attractionId] = preferences.Counts.GetValueOrDefault(attractionId) + 5;
                break;
        }
        await SaveCountsAsync(userId, tenantId, preferences.Counts, cancellationToken);
        await SaveExcludedAsync(userId, tenantId, preferences.Excluded, cancellationToken);
        return new ServiceResult(200, "反馈已记录。", new { attractionId, choice });
    }

    private async Task<Preferences> LoadPreferencesAsync(long homeId, CancellationToken cancellationToken)
    {
        var interests = new List<string>();
        var excluded = new HashSet<long>();
        var counts = new Dictionary<long, int>();
        var result = await _knowledge.ListAsync(homeId, TravelCategory, cancellationToken);
        if (result.Succeeded && result.Data is IEnumerable<FamilyKnowledgeView> rows)
        {
            var byKey = rows.Where(x => x.Key is not null).ToDictionary(x => x.Key!, x => x.Value ?? "");
            if (byKey.TryGetValue(InterestsKey, out var interestsJson)) ParseInterests(interestsJson, interests);
            if (byKey.TryGetValue(ExcludedKey, out var excludedJson)) ParseIds(excludedJson, excluded);
            if (byKey.TryGetValue(CountsKey, out var countsJson)) ParseCounts(countsJson, counts);
        }
        return new Preferences(interests, excluded, counts);
    }

    private static void ParseInterests(string json, List<string> target)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                        target.Add(item.GetString()!);
                }
            }
        }
        catch (JsonException) { }
    }

    private static void ParseIds(string json, HashSet<long> target)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.TryGetInt64(out var id)) target.Add(id);
                }
            }
        }
        catch (JsonException) { }
    }

    private static void ParseCounts(string json, Dictionary<long, int> target)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (long.TryParse(property.Name, out var id) && property.Value.TryGetInt32(out var count))
                        target[id] = count;
                }
            }
        }
        catch (JsonException) { }
    }

    private async Task SaveCountsAsync(long userId, long homeId, Dictionary<long, int> counts, CancellationToken cancellationToken) =>
        await WritePreferenceAsync(userId, homeId, CountsKey, JsonSerializer.Serialize(counts), cancellationToken);

    private async Task SaveExcludedAsync(long userId, long homeId, HashSet<long> excluded, CancellationToken cancellationToken) =>
        await WritePreferenceAsync(userId, homeId, ExcludedKey, JsonSerializer.Serialize(excluded.ToArray()), cancellationToken);

    private async Task WritePreferenceAsync(long userId, long homeId, string key, string value, CancellationToken cancellationToken)
    {
        var request = new FamilyKnowledgeWriteRequest
        {
            Category = TravelCategory,
            Key = key,
            Value = value,
            SourceType = FamilyKnowledgeSourceType.SystemAi,
            ConfidenceScore = 0.9m,
            ConflictResolutionStrategy = FamilyKnowledgeConflictResolutionStrategy.Latest
        };
        await _knowledge.WriteAsync(homeId, userId, request, cancellationToken);
    }

    private static bool MatchesInterests(TravelAttraction attraction, IReadOnlyList<string> interests)
    {
        var tags = ReadTags(attraction);
        return interests.Any(interest =>
            tags.Any(tag => tag.Contains(interest, StringComparison.OrdinalIgnoreCase))
            || attraction.Category.Contains(interest, StringComparison.OrdinalIgnoreCase)
            || attraction.Name.Contains(interest, StringComparison.OrdinalIgnoreCase)
            || (attraction.Description?.Contains(interest, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static IReadOnlyList<string> ReadTags(TravelAttraction attraction)
    {
        if (string.IsNullOrWhiteSpace(attraction.TagsJson)) return [];
        try
        {
            using var document = JsonDocument.Parse(attraction.TagsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            var tags = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) tags.Add(item.GetString()!);
            }
            return tags;
        }
        catch (JsonException) { return []; }
    }

    private static string BuildReason(TravelAttraction attraction)
    {
        var weather = string.IsNullOrWhiteSpace(attraction.WeatherTag) ? "" : $"{attraction.WeatherTag}，";
        var description = string.IsNullOrWhiteSpace(attraction.Description) ? "" : attraction.Description;
        return $"「{attraction.Name}」{attraction.Category}类，建议游玩约 {attraction.DurationHours} 小时，消费档 {attraction.CostLevel}/5。{weather}{description}";
    }

    private sealed record Preferences(List<string> Interests, HashSet<long> Excluded, Dictionary<long, int> Counts);
}
