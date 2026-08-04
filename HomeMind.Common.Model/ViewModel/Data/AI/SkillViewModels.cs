namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>新建或更新 AI 技能的请求参数。</summary>
public sealed record SkillRequest(string? Name, string? Prompt, string? Scopes, bool? IsActive);
