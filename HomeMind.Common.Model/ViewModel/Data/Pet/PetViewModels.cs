namespace HomeMind.Common.Model.ViewModel.Data.Pet;

/// <summary>创建宠物档案请求。</summary>
public sealed record PetCreateRequest(string Name, string Species, string? Breed = null, DateTime? BirthDate = null, string? Notes = null);
/// <summary>宠物档案视图。</summary>
public sealed record PetView(long Id, string Name, string Species, string? Breed, DateTime? BirthDate, string? Notes, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
/// <summary>创建宠物照护日历请求。</summary>
public sealed record PetCareEventCreateRequest(string CareType, string Title, DateTime DueDate, string? Notes = null);
/// <summary>宠物照护日历视图。</summary>
public sealed record PetCareEventView(long Id, long PetId, string CareType, string Title, DateTime DueDate, DateTime? CompletedAt, string? Notes);
/// <summary>创建或更新宠物用品库存请求。</summary>
public sealed record PetSupplyUpsertRequest(string ItemName, decimal Quantity, decimal DailyUsage, string Unit = "份", DateTime? MeasuredAt = null, string SourceType = "manual");
/// <summary>宠物用品库存视图。</summary>
public sealed record PetSupplyView(long Id, long PetId, string ItemName, decimal Quantity, decimal DailyUsage, string Unit, string SourceType, DateTime MeasuredAt, decimal? DaysRemaining, long? ConfirmationId);
