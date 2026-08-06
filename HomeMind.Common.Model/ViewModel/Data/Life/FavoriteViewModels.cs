namespace HomeMind.Common.Model.ViewModel.Data.Life;

/// <summary>个人偏好收藏视图。</summary>
/// <param name="Id">收藏主键。</param>
/// <param name="OwnerMemberId">归属家庭成员主键。</param>
/// <param name="Category">收藏分类，restaurant / travel / material。</param>
/// <param name="Name">店铺/地点/素材名称。</param>
/// <param name="DetailJson">结构化扩展信息 JSON，可为空。</param>
/// <param name="Visibility">可见性，private / family。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record FavoriteView(long Id, long OwnerMemberId, string Category, string Name, string? DetailJson, string Visibility, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>创建收藏的请求体。</summary>
/// <param name="Category">收藏分类，restaurant / travel / material。</param>
/// <param name="Name">店铺/地点/素材名称，长度 1-128。</param>
/// <param name="DetailJson">结构化扩展信息 JSON，可为空；建议包含 cuisine/address/lat/lng/tags/note。</param>
/// <param name="Visibility">可见性，private / family，默认 private。</param>
/// <param name="OwnerMemberId">归属家庭成员主键，可为空；为空时默认解析为当前成员。</param>
public sealed record FavoriteCreateRequest(string Category, string Name, string? DetailJson = null, string Visibility = "private", long? OwnerMemberId = null);

/// <summary>更新收藏的请求体；仅可更新本人或家庭管理员拥有的收藏。</summary>
/// <param name="Name">店铺/地点/素材名称，长度 1-128。</param>
/// <param name="DetailJson">结构化扩展信息 JSON，可为空。</param>
/// <param name="Visibility">可见性，private / family。</param>
public sealed record FavoriteUpdateRequest(string Name, string? DetailJson = null, string Visibility = "private");

/// <summary>对话导入收藏的请求体；AI 提取部分依赖 AI 运行时，按部署环境验证。</summary>
/// <param name="Category">收藏分类，restaurant / travel / material。</param>
/// <param name="Name">店铺/地点/素材名称，长度 1-128。</param>
/// <param name="DetailJson">结构化扩展信息 JSON，可为空。</param>
/// <param name="Visibility">可见性，private / family，默认 private。</param>
/// <param name="Source">来源留痕，例如"小红书""大众点评"或"对话"。</param>
/// <param name="ConversationText">原始对话文本，可为空；记录来源语义，不参与确定性解析。</param>
public sealed record FavoriteImportRequest(string Category, string Name, string? DetailJson = null, string Visibility = "private", string? Source = null, string? ConversationText = null);
