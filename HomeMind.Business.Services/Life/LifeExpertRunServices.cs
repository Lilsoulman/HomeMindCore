using System.Text.Json;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.IServices.Life;
using HomeMind.Business.IServices.Productivity;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.Family;
using HomeMind.Common.Model.Entities.Life;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Life;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Business.Services.Life;

/// <summary>
/// 个人生活专家确定性编排：翻牌（recommend）读取个人偏好收藏并按时间/位置/口味给出 Top1-2 建议（只读 L1）；
/// 行程（plan）结合目的地/天数/偏好、私藏库与 Mock 天气生成每日安排，并产出
/// <c>calendar_create_event</c> Run Action（L1 确认后经日历服务执行）。所有运行复用既有
/// AgentRun、确认、幂等与审计边界，不新建运行时。
/// </summary>
public sealed class LifeExpertRunServices : ILifeExpertRunServices
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedIntents = new(StringComparer.Ordinal) { "recommend", "plan" };
    private const int TopRecommendationCount = 2;
    private const int MaxPlanDays = 7;

    private readonly HomeMindDbContext _db;
    private readonly ICalendarServices _calendar;

    /// <summary>构造个人生活专家运行服务。</summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="calendar">日历服务，用于行程动作确认后的日历事件创建。</param>
    public LifeExpertRunServices(HomeMindDbContext db, ICalendarServices calendar)
    {
        _db = db;
        _calendar = calendar;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(long userId, long tenantId, LifeExpertRunRequest request, CancellationToken cancellationToken = default)
    {
        var intent = request.Intent?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(intent) || !AllowedIntents.Contains(intent))
            return new ServiceResult(422, "仅支持 recommend 翻牌或 plan 行程意图。");

        var version = await FindLifeExpertVersionAsync(cancellationToken);
        if (version is null) return new ServiceResult(503, "个人生活专家尚未初始化，请先应用数据库迁移 017。");

        if (!IsValidJson(request.InputJson))
            return new ServiceResult(422, "运行输入必须为合法 JSON。");

        var idempotencyKey = Guid.TryParse(request.IdempotencyKey, out var parsedKey) ? parsedKey.ToString() : Guid.NewGuid().ToString();
        var existing = await _db.AgentRuns.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.RequestIdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.ExpertVersionId != version.Id) return new ServiceResult(409, "该幂等键已用于其他专家运行。");
            return new ServiceResult(200, "个人生活专家运行已存在。", await ToViewAsync(existing, cancellationToken));
        }

        var now = DateTime.UtcNow;
        var run = new AgentRun
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "expert",
            ExpertVersionId = version.Id,
            RequestIdempotencyKey = idempotencyKey,
            Input = request.InputJson,
            Status = "planning",
            Mode = HousekeeperRunPolicies.Steward,
            AutoConfirmPolicy = HousekeeperRunPolicies.L3Only,
            EstimatedCredits = version.EstimatedCredits,
            StartedAt = now,
            CreatedAt = now
        };
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        return intent == "recommend"
            ? await RunRecommendAsync(userId, tenantId, run, request.InputJson, cancellationToken)
            : await RunPlanAsync(userId, tenantId, run, request.InputJson, cancellationToken);
    }

    /// <summary>确定性翻牌：按口味（tags/cuisine）、位置（address）与时段评分，返回 Top1-2 建议（只读 L1）。</summary>
    private async Task<ServiceResult> RunRecommendAsync(long userId, long tenantId, AgentRun run, string inputJson, CancellationToken cancellationToken)
    {
        var input = ReadRecommendInput(inputJson);
        AddEvent(run, 1, "running", "正在检索个人偏好收藏。", DateTime.UtcNow);
        var owner = await ResolveOwnerMemberAsync(tenantId, userId, cancellationToken);
        var favorites = await _db.PersonalFavorites
            .Where(x => x.HomeId == tenantId && x.DeletedAt == null && x.Category == PersonalFavoriteCategory.Restaurant)
            .ToListAsync(cancellationToken);
        var visible = favorites.Where(x => x.Visibility == PersonalFavoriteVisibility.Family || (owner is not null && x.OwnerMemberId == owner.Id)).ToArray();

        var recommendations = RankRecommendations(input, visible).Take(TopRecommendationCount).ToArray();
        AddEvent(run, 2, "recommendations_ready", $"已筛选 {recommendations.Length} 家候选店铺。", DateTime.UtcNow);

        var now = DateTime.UtcNow;
        run.Status = "completed";
        run.FinishedAt = now;
        run.ResultSummary = recommendations.Length == 0
            ? "当前偏好库中没有匹配的店铺建议。"
            : $"为你推荐 {recommendations.Length} 家店铺。";
        run.Result = JsonSerializer.Serialize(new
        {
            intent = "recommend",
            candidateCount = visible.Length,
            recommendations = recommendations.Select(x => new { x.FavoriteId, x.Name, x.Reason, x.Tags })
        });
        AddEvent(run, 3, "completed", run.ResultSummary, now);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "翻牌建议已生成。", await ToViewAsync(run, cancellationToken));
    }

    /// <summary>确定性行程规划：结合目的地/天数/偏好、私藏库与 Mock 天气生成每日安排，产出待确认的日历同步动作。</summary>
    private async Task<ServiceResult> RunPlanAsync(long userId, long tenantId, AgentRun run, string inputJson, CancellationToken cancellationToken)
    {
        var input = ReadPlanInput(inputJson);
        if (input is null) return new ServiceResult(422, "行程输入必须包含 1-64 字符的目的地，天数 1-7。");

        AddEvent(run, 1, "running", "正在规划行程并检索私藏库。", DateTime.UtcNow);
        var owner = await ResolveOwnerMemberAsync(tenantId, userId, cancellationToken);
        var favorites = await _db.PersonalFavorites
            .Where(x => x.HomeId == tenantId && x.DeletedAt == null && (x.Category == PersonalFavoriteCategory.Travel || x.Category == PersonalFavoriteCategory.Restaurant))
            .ToListAsync(cancellationToken);
        var visible = favorites.Where(x => x.Visibility == PersonalFavoriteVisibility.Family || (owner is not null && x.OwnerMemberId == owner.Id)).ToArray();
        var travels = visible.Where(x => x.Category == PersonalFavoriteCategory.Travel).ToArray();
        var restaurants = visible.Where(x => x.Category == PersonalFavoriteCategory.Restaurant).ToArray();

        var dayPlans = BuildDayPlans(input.Destination, input.Days, travels, restaurants);
        AddEvent(run, 2, "plan_ready", $"已生成 {dayPlans.Count} 天行程安排。", DateTime.UtcNow);

        var planJson = JsonSerializer.Serialize(new { destination = input.Destination, days = dayPlans }, JsonOptions);
        var now = DateTime.UtcNow;
        _db.ExpertRunActions.Add(new ExpertRunAction
        {
            RunId = run.Id,
            TenantId = tenantId,
            UserId = userId,
            ActionType = "calendar_create_event",
            RequestIdempotencyKey = Guid.NewGuid().ToString(),
            RequestJson = planJson,
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now
        });

        run.Status = "pending_actions";
        run.ResultSummary = $"已生成 {dayPlans.Count} 天行程（{input.Destination}），确认后同步日历。";
        run.Result = JsonSerializer.Serialize(new { intent = "plan", destination = input.Destination, dayCount = dayPlans.Count });
        AddEvent(run, 3, "pending_actions", run.ResultSummary, now);
        await _db.SaveChangesAsync(cancellationToken);
        return new ServiceResult(201, "行程规划已生成，请确认后同步日历。", await ToViewAsync(run, cancellationToken));
    }

    /// <summary>按天生成行程安排：上午/下午/晚上引用旅行收藏，天气为确定性 Mock（晴/阴/雨轮换）。</summary>
    private static IReadOnlyList<DayPlan> BuildDayPlans(string destination, int days, PersonalFavorite[] travels, PersonalFavorite[] restaurants)
    {
        var plans = new List<DayPlan>(days);
        for (var day = 1; day <= days; day++)
        {
            var weather = MockWeather(day - 1);
            var activities = new List<PlanActivity>();
            var morning = travels.Length > 0 ? travels[(day - 1) % travels.Length] : null;
            var afternoon = travels.Length > 0 ? travels[day % travels.Length] : null;
            var dinner = restaurants.Length > 0 ? restaurants[(day - 1) % restaurants.Length] : null;
            if (morning is not null) activities.Add(new PlanActivity("上午", morning.Name, weather == "雨" ? "雨天建议室内游览，注意防雨。" : $"天气{weather}，适合游览。"));
            if (afternoon is not null) activities.Add(new PlanActivity("下午", afternoon.Name, weather == "雨" ? "雨天改为室内备选行程。" : $"天气{weather}，下午行程如常。"));
            activities.Add(new PlanActivity("晚上", dinner?.Name ?? "自由安排", dinner is not null ? $"晚餐推荐来自私藏库：{dinner.Name}。" : "晚餐自由安排。"));
            plans.Add(new DayPlan(day, weather, activities));
        }
        return plans;
    }

    /// <summary>确定性 Mock 天气：按天序号轮换晴/阴/雨。</summary>
    private static string MockWeather(int dayIndex) => (dayIndex % 3) switch
    {
        0 => "晴",
        1 => "阴",
        _ => "雨"
    };

    /// <inheritdoc />
    public async Task<ServiceResult> ConfirmActionAsync(long userId, long tenantId, long runId, long actionId, ConfirmLifeExpertActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.IdempotencyKey, out _))
            return new ServiceResult(422, "确认行程动作时必须提供有效的幂等键。");

        var action = await _db.ExpertRunActions.SingleOrDefaultAsync(x =>
            x.Id == actionId && x.RunId == runId && x.TenantId == tenantId && x.UserId == userId && x.ActionType == "calendar_create_event", cancellationToken);
        if (action is null) return new ServiceResult(404, "请求的行程动作不存在。");

        var idempotencyKey = request.IdempotencyKey;
        var previous = await _db.ActionExecutionAudits.SingleOrDefaultAsync(
            x => x.RunActionId == action.Id && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (previous is not null) return ReplayActionResult(action, previous);
        if (action.Status != "pending") return new ServiceResult(409, "该行程动作已经确认或处理完成，不能再次执行。");

        var plan = ReadPlan(action.RequestJson);
        if (plan is null) return new ServiceResult(422, "行程动作内容无效。");

        var now = DateTime.UtcNow;
        action.Status = "executing";
        action.UpdatedAt = now;
        var audit = new ActionExecutionAudit
        {
            TenantId = tenantId,
            RunActionId = action.Id,
            OperatorUserId = userId,
            IdempotencyKey = idempotencyKey,
            Status = "executing",
            Command = JsonSerializer.Serialize(new { destination = plan.Destination, dayCount = plan.Days.Count }),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ActionExecutionAudits.Add(audit);
        var run = await _db.AgentRuns.SingleAsync(x => x.Id == runId, cancellationToken);
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), "action_confirmed", $"已确认同步行程：{plan.Destination}。", now);
        await _db.SaveChangesAsync(cancellationToken);

        var succeeded = true;
        string? failureMessage = null;
        foreach (var day in plan.Days)
        {
            var start = DateTime.UtcNow.Date.AddDays(day.Day - 1).AddHours(9);
            var result = await _calendar.CreateEventAsync(userId, tenantId, new CalendarEventRequest(
                Title: $"{plan.Destination} 行程 D{day.Day}",
                Description: BuildDayDescription(day),
                Location: plan.Destination,
                StartAt: start,
                EndAt: start.AddHours(10),
                Timezone: "Asia/Shanghai",
                AllDay: false,
                Color: null,
                Opacity: null,
                RepeatRule: null), cancellationToken);
            if (!result.Succeeded)
            {
                succeeded = false;
                failureMessage = result.Message;
                break;
            }
        }

        now = DateTime.UtcNow;
        action.Status = succeeded ? "executed" : "failed";
        action.Result = JsonSerializer.Serialize(new { status = action.Status, errorCode = succeeded ? null : "calendar_create_failed" });
        action.UpdatedAt = now;
        audit.Status = action.Status;
        audit.Result = JsonSerializer.Serialize(new { status = action.Status, errorCode = succeeded ? null : "calendar_create_failed" });
        audit.UpdatedAt = now;
        AddEvent(run, await NextSequenceAsync(runId, cancellationToken), succeeded ? "action_executed" : "action_failed", succeeded ? $"行程已同步日历：{plan.Destination}。" : $"行程同步失败：{failureMessage ?? "日历服务不可用"}。", now);
        await _db.SaveChangesAsync(cancellationToken);
        return succeeded
            ? new ServiceResult(200, "行程已同步日历。", new { actionId = action.Id, status = action.Status, message = "行程已同步日历。" })
            : new ServiceResult(502, failureMessage ?? "日历服务暂时不可用。");
    }

    private static ServiceResult ReplayActionResult(ExpertRunAction action, ActionExecutionAudit audit)
    {
        var succeeded = audit.Status == "executed";
        return new ServiceResult(succeeded ? 200 : audit.Status == "executing" ? 202 : 502, succeeded ? "行程已同步日历。" : "行程动作正在处理或已执行失败。", new { actionId = action.Id, status = action.Status, message = succeeded ? "行程已同步日历。" : "行程动作正在处理或已执行失败。" });
    }

    private static string BuildDayDescription(DayPlan day) =>
        $"天气：{day.Weather}。" + string.Join("；", day.Activities.Select(x => $"{x.TimeSlot}：{x.Name}（{x.Note}）"));

    private static PlanInput? ReadPlanInput(string inputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(inputJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var destination = root.TryGetProperty("destination", out var destinationElement) ? destinationElement.GetString()?.Trim() : null;
            var days = root.TryGetProperty("days", out var daysElement) && daysElement.TryGetInt32(out var parsedDays) ? parsedDays : 1;
            if (string.IsNullOrWhiteSpace(destination) || destination.Length > 64 || days is < 1 or > MaxPlanDays) return null;
            return new PlanInput(destination, days);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Plan? ReadPlan(string requestJson)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<PlanDto>(requestJson, JsonOptions);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Destination) || dto.Days is null) return null;
            var dayPlans = dto.Days.Where(x => x is not null).Select(x => new DayPlan(
                x!.Day,
                x.Weather ?? "",
                (x.Activities ?? []).Where(a => a is not null).Select(a => new PlanActivity(a!.TimeSlot ?? "", a.Name ?? "", a.Note ?? "")).ToArray())).ToArray();
            return new Plan(dto.Destination, dayPlans);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<int> NextSequenceAsync(long runId, CancellationToken cancellationToken) =>
        (await _db.RunEvents.Where(x => x.RunId == runId).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1;

    /// <summary>按口味（tags/cuisine 匹配）、位置（address 匹配）与时段偏好确定性评分。</summary>
    private static IEnumerable<LifeExpertRecommendationView> RankRecommendations(RecommendInput input, PersonalFavorite[] favorites)
    {
        return favorites.Select(favorite =>
        {
            var detail = ReadDetail(favorite);
            var tags = detail.Tags;
            var cuisine = detail.Cuisine;
            var address = detail.Address;
            var score = 0;
            var reasons = new List<string>();

            if (HasMatch(tags, input.Taste) || Matches(cuisine, input.Taste))
            {
                score += 2;
                reasons.Add($"口味匹配“{input.Taste}”");
            }
            if (Matches(address, input.Location))
            {
                score += 1;
                reasons.Add($"位置匹配“{input.Location}”");
            }
            if (ReasonsForTime(tags, cuisine, input.Time) is { } timeReason)
            {
                score += 1;
                reasons.Add(timeReason);
            }

            var reason = reasons.Count == 0 ? "来自你的私藏店铺库" : string.Join("，", reasons);
            return (favorite, score, reason, tags);
        })
        .OrderByDescending(x => x.score)
        .ThenByDescending(x => x.favorite.UpdatedAt)
        .Select(x => new LifeExpertRecommendationView(x.favorite.Id, x.favorite.Name, x.reason, x.tags));
    }

    /// <summary>按输入时段给出推荐理由；无法判断时段返回 null。</summary>
    private static string? ReasonsForTime(IReadOnlyList<string> tags, string? cuisine, string? time)
    {
        var normalized = time?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "morning" when HasMatch(tags, "早餐") || Matches(cuisine, "早") => "适合早餐时段",
            "noon" when HasMatch(tags, "午餐") || Matches(cuisine, "午") => "适合午餐时段",
            "evening" when HasMatch(tags, "晚餐") || Matches(cuisine, "晚") => "适合晚餐时段",
            _ => null
        };
    }

    private static bool HasMatch(IReadOnlyList<string> tags, string? keyword) =>
        !string.IsNullOrWhiteSpace(keyword) && tags.Any(tag => tag.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool Matches(string? field, string? keyword) =>
        !string.IsNullOrWhiteSpace(field) && !string.IsNullOrWhiteSpace(keyword) && field.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);

    private static FavoriteDetail ReadDetail(PersonalFavorite favorite)
    {
        if (string.IsNullOrWhiteSpace(favorite.DetailJson)) return new FavoriteDetail([], null, null);
        try
        {
            using var document = JsonDocument.Parse(favorite.DetailJson);
            var root = document.RootElement;
            var tags = root.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array
                ? tagsElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray()
                : [];
            var cuisine = root.TryGetProperty("cuisine", out var cuisineElement) ? cuisineElement.GetString() : null;
            var address = root.TryGetProperty("address", out var addressElement) ? addressElement.GetString() : null;
            return new FavoriteDetail(tags, cuisine, address);
        }
        catch (JsonException)
        {
            return new FavoriteDetail([], null, null);
        }
    }

    /// <summary>解析当前用户的归属成员：优先取该用户创建的成员，其次家庭主用户。</summary>
    private async Task<FamilyMember?> ResolveOwnerMemberAsync(long homeId, long actorUserId, CancellationToken cancellationToken) =>
        await _db.FamilyMembers
            .Where(x => x.HomeId == homeId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedByUserId == actorUserId ? 1 : 0)
            .ThenByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ExpertVersion?> FindLifeExpertVersionAsync(CancellationToken cancellationToken) =>
        await (from expert in _db.Experts
               join version in _db.ExpertVersions on expert.Id equals version.ExpertId
               where expert.TenantId == 1 && expert.Code == "personal-life-expert" && expert.Status == "active" && version.Status == "published"
               orderby version.Version descending
               select version).FirstOrDefaultAsync(cancellationToken);

    private static bool IsValidJson(string inputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(inputJson);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>解析翻牌输入；字段缺失时按无匹配处理。</summary>
    private static RecommendInput ReadRecommendInput(string inputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(inputJson);
            var root = document.RootElement;
            return new RecommendInput(
                root.TryGetProperty("time", out var time) ? time.GetString() : null,
                root.TryGetProperty("location", out var location) ? location.GetString() : null,
                root.TryGetProperty("taste", out var taste) ? taste.GetString() : null);
        }
        catch (JsonException)
        {
            return new RecommendInput(null, null, null);
        }
    }

    private void AddEvent(AgentRun run, int sequence, string type, string message, DateTime createdAt) =>
        _db.RunEvents.Add(new RunEvent
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            Sequence = sequence,
            EventType = type,
            Payload = JsonSerializer.Serialize(new { message }),
            CreatedAt = createdAt
        });

    private async Task<LifeExpertRunView> ToViewAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var events = await _db.RunEvents
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        var actions = await _db.ExpertRunActions
            .Where(x => x.RunId == run.Id && x.TenantId == run.TenantId && x.ActionType == "calendar_create_event")
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return new LifeExpertRunView(
            run.Id,
            run.Status,
            run.ResultSummary,
            run.CreatedAt,
            run.FinishedAt,
            events.Select(x => new LifeExpertRunEventView(x.Sequence, x.EventType, ReadMessage(x.Payload), x.CreatedAt)).ToArray(),
            ReadRecommendations(run.Result),
            actions.Select(x => new LifeExpertActionView(x.Id, x.ActionType, x.Status, ReadPlanTitle(x.RequestJson), ReadPlanSummary(x.RequestJson), "L1")).ToArray());
    }

    /// <summary>读取行程动作标题；内容非法时回退为默认标题。</summary>
    private static string ReadPlanTitle(string requestJson)
    {
        var plan = ReadPlan(requestJson);
        return plan is null ? "同步行程到日历" : $"{plan.Destination} 行程同步";
    }

    /// <summary>读取行程动作摘要；内容非法时回退为默认说明。</summary>
    private static string ReadPlanSummary(string requestJson)
    {
        var plan = ReadPlan(requestJson);
        return plan is null ? "确认后生成每日日历事件。" : $"共 {plan.Days.Count} 天（{plan.Days.Count} 个日历事件），确认后同步。";
    }

    /// <summary>从运行结果 JSON 解析展示安全的翻牌建议；结果缺失或非法时返回空数组。</summary>
    private static IReadOnlyList<LifeExpertRecommendationView> ReadRecommendations(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return [];
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            if (!document.RootElement.TryGetProperty("recommendations", out var recommendations) || recommendations.ValueKind != JsonValueKind.Array)
                return [];
            return recommendations.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x => new LifeExpertRecommendationView(
                x.TryGetProperty("FavoriteId", out var id) ? id.GetInt64() : 0,
                x.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
                x.TryGetProperty("Reason", out var reason) ? reason.GetString() ?? "" : "",
                x.TryGetProperty("Tags", out var tags) && tags.ValueKind == JsonValueKind.Array
                    ? tags.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()!).ToArray()
                    : [])).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ReadMessage(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "";
    }

    private sealed record RecommendInput(string? Time, string? Location, string? Taste);
    private sealed record FavoriteDetail(IReadOnlyList<string> Tags, string? Cuisine, string? Address);
    private sealed record PlanInput(string Destination, int Days);
    private sealed record Plan(string Destination, IReadOnlyList<DayPlan> Days);
    private sealed record DayPlan(int Day, string Weather, IReadOnlyList<PlanActivity> Activities);
    private sealed record PlanActivity(string TimeSlot, string Name, string Note);
    private sealed record PlanDto(string? Destination, IReadOnlyList<PlanDayDto>? Days);
    private sealed record PlanDayDto(int Day, string? Weather, IReadOnlyList<PlanActivityDto>? Activities);
    private sealed record PlanActivityDto(string? TimeSlot, string? Name, string? Note);
}
