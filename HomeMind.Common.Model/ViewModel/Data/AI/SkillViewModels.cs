namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>新建或更新 AI 技能的请求参数。</summary>
/// <param name="Name">技能展示名，可空表示不修改。</param>
/// <param name="Prompt">技能系统提示词，可空表示不修改。</param>
/// <param name="Scopes">技能所需授权范围 JSON 字符串，可空表示不修改。</param>
/// <param name="IsActive">是否启用，可空表示不修改。</param>
public sealed record SkillRequest(string? Name, string? Prompt, string? Scopes, bool? IsActive);

/// <summary>平台级 Skill 目录展示项，不包含实现提示词或运行期内部配置。</summary>
/// <param name="Key">平台 Skill 业务键。</param>
/// <param name="Name">展示名称。</param>
/// <param name="Category">分类。</param>
/// <param name="Description">展示说明。</param>
/// <param name="RiskLevel">静态风险等级。</param>
/// <param name="RequiredPermission">调用所需最小权限。</param>
/// <param name="InputSchema">输入 JSON Schema。</param>
/// <param name="Status">目录状态。</param>
public sealed record PlatformSkillView(string Key, string Name, string Category, string? Description, string RiskLevel, string RequiredPermission, string InputSchema, string Status);

/// <summary>租户成员 Skill 脱敏摘要，不包含 Prompt 或授权范围细节。</summary>
/// <param name="Id">用户 Skill 主键。</param>
/// <param name="Name">展示名称。</param>
/// <param name="IsActive">是否启用。</param>
/// <param name="MemberName">创建成员展示名。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="UpdatedAt">更新时间（UTC）。</param>
public sealed record MemberSkillSummaryView(long Id, string Name, bool IsActive, string MemberName, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>开发端 Skill 目录聚合视图：平台目录与当前租户成员 Skill 摘要。</summary>
/// <param name="PlatformSkills">平台级目录。</param>
/// <param name="MemberSkills">租户成员的脱敏用户 Skill 摘要。</param>
public sealed record AllSkillsView(IReadOnlyList<PlatformSkillView> PlatformSkills, IReadOnlyList<MemberSkillSummaryView> MemberSkills);
