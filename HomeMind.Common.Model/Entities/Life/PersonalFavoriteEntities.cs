using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities.Life;

/// <summary>个人偏好收藏实体，支撑个人生活专家的探店翻牌与行程规划；默认仅归属成员本人可见。</summary>
[Table("personal_favorites")]
public sealed class PersonalFavorite
{
    /// <summary>收藏主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>所属家庭主键，由 JWT 推导，客户端不可覆盖。</summary>
    [Column("home_id")] public long HomeId { get; set; }
    /// <summary>归属家庭成员主键，默认取当前 JWT 成员。</summary>
    [Column("owner_member_id")] public long OwnerMemberId { get; set; }
    /// <summary>收藏分类，参见 <see cref="PersonalFavoriteCategory"/>。</summary>
    [Column("category")] public string Category { get; set; } = null!;
    /// <summary>店铺/地点/素材名称，列表展示用。</summary>
    [Column("name")] public string Name { get; set; } = null!;
    /// <summary>结构化扩展信息 JSON：cuisine/address/lat/lng/tags/note/source。</summary>
    [Column("detail_json")] public string? DetailJson { get; set; }
    /// <summary>可见性，参见 <see cref="PersonalFavoriteVisibility"/>；private 仅本人可读写。</summary>
    [Column("visibility")] public string Visibility { get; set; } = PersonalFavoriteVisibility.Private;
    /// <summary>软删除时间戳。</summary>
    [Column("deleted_at")] public DateTime? DeletedAt { get; set; }
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>乐观锁版本号。</summary>
    [Column("row_version")] public long RowVersion { get; set; } = 1;
}

/// <summary>个人偏好收藏分类集合。</summary>
public static class PersonalFavoriteCategory
{
    /// <summary>店铺收藏，支撑探店翻牌。</summary>
    public const string Restaurant = "restaurant";
    /// <summary>旅行地点收藏，支撑行程规划。</summary>
    public const string Travel = "travel";
    /// <summary>短视频素材收藏，为后续版本预留。</summary>
    public const string Material = "material";

    /// <summary>分类是否合法。</summary>
    /// <param name="category">待校验分类。</param>
    /// <returns>合法为 true。</returns>
    public static bool IsValid(string? category) => category is Restaurant or Travel or Material;
}

/// <summary>周末出行景点库实体，支撑出行推荐专家；种子数据本地维护，天气标签手动更新。</summary>
[Table("attractions")]
public sealed class TravelAttraction
{
    /// <summary>景点主键。</summary>
    [Key, Column("id")] public long Id { get; set; }
    /// <summary>景点名称。</summary>
    [Column("name", TypeName = "varchar(128)")] public string Name { get; set; } = null!;
    /// <summary>所在城市/区域。</summary>
    [Column("city", TypeName = "varchar(64)")] public string City { get; set; } = null!;
    /// <summary>分类：自然/人文/亲子/美食/商圈。</summary>
    [Column("category", TypeName = "varchar(32)")] public string Category { get; set; } = null!;
    /// <summary>建议游玩时长（小时）。</summary>
    [Column("duration_hours", TypeName = "decimal(3,1)")] public decimal DurationHours { get; set; }
    /// <summary>消费档位 1~5。</summary>
    [Column("cost_level")] public byte CostLevel { get; set; }
    /// <summary>手动维护的天气标签，如"晴天开阔""雨天室内"。</summary>
    [Column("weather_tag", TypeName = "varchar(32)")] public string? WeatherTag { get; set; }
    /// <summary>兴趣标签 JSON 数组，如 ["拍照","亲子","爬山"]。</summary>
    [Column("tags_json")] public string? TagsJson { get; set; }
    /// <summary>纬度，预留。</summary>
    [Column("latitude", TypeName = "decimal(9,6)")] public decimal? Latitude { get; set; }
    /// <summary>经度，预留。</summary>
    [Column("longitude", TypeName = "decimal(9,6)")] public decimal? Longitude { get; set; }
    /// <summary>一句话简介。</summary>
    [Column("description", TypeName = "varchar(512)")] public string? Description { get; set; }
    /// <summary>是否可被推荐。</summary>
    [Column("is_active")] public bool IsActive { get; set; } = true;
    /// <summary>创建时间（UTC）。</summary>
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    /// <summary>更新时间（UTC）。</summary>
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>个人偏好收藏可见性集合。</summary>
public static class PersonalFavoriteVisibility
{
    /// <summary>仅归属成员本人可读写。</summary>
    public const string Private = "private";
    /// <summary>家庭内可读，写仍限本人或家庭管理员。</summary>
    public const string Family = "family";

    /// <summary>可见性是否合法。</summary>
    /// <param name="visibility">待校验可见性。</param>
    /// <returns>合法为 true。</returns>
    public static bool IsValid(string? visibility) => visibility is Private or Family;
}
