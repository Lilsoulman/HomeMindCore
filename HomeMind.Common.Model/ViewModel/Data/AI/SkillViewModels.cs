namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>新建或更新 AI 技能的请求参数。</summary>
/// <param name="Name">技能展示名，可空表示不修改。</param>
/// <param name="Prompt">技能系统提示词，可空表示不修改。</param>
/// <param name="Scopes">技能所需授权范围 JSON 字符串，可空表示不修改。</param>
/// <param name="IsActive">是否启用，可空表示不修改。</param>
public sealed record SkillRequest(string? Name, string? Prompt, string? Scopes, bool? IsActive);
