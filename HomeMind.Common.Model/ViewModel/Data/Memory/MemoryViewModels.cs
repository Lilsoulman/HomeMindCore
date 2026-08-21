namespace HomeMind.Common.Model.ViewModel.Data.Memory;

/// <summary>记忆候选的安全展示视图。</summary>
public sealed record MemoryCandidateView(
    long Id,
    string Kind,
    string Visibility,
    string Key,
    string ProposedValue,
    string DisplaySummary,
    decimal Confidence,
    string RiskLevel,
    string Status,
    long? SourceRunId,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

/// <summary>接受候选时可修改展示值的请求。</summary>
public sealed class ResolveMemoryCandidateRequest
{
    /// <summary>可选的替代值；为空时接受候选原值。</summary>
    public string? Value { get; init; }
    /// <summary>可选的替代展示摘要；为空时使用候选摘要。</summary>
    public string? DisplaySummary { get; init; }
}

/// <summary>学习记忆库的安全展示视图。</summary>
public sealed record LearningMemoryView(
    long Id,
    string Summary,
    string Kind,
    string Visibility,
    decimal Stability,
    string Status,
    DateTime LearnedAt,
    DateTime? ExpiresAt,
    IReadOnlyList<LearningMemorySourceReferenceView> SourceReferences,
    int RestrictedReferenceCount,
    string ResolutionSummary);

/// <summary>学习记忆可公开的来源引用。</summary>
public sealed record LearningMemorySourceReferenceView(string Type, long Id);

/// <summary>游标分页学习记忆结果。</summary>
public sealed record LearningMemoryPageView(IReadOnlyList<LearningMemoryView> Items, string? NextCursor);
