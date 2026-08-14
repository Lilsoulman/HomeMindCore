using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>专家目录列表项视图；Source 标识来源（basic=平台基础，mine=本人自建），不暴露他人所有者。</summary>
public sealed record ExpertCatalogItemView(
    long Id,
    string CatalogType,
    string Source,
    string Code,
    string Name,
    string Category,
    string? Description,
    decimal EstimatedCredits);

/// <summary>专家详情视图；含最新已发布版本快照，仅本人可见自建专家详情。</summary>
public sealed record ExpertDetailView(
    long Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    string? PrivacyScope,
    string Source,
    long VersionId,
    int Version,
    string Persona,
    string Methodology,
    string PromptTemplate,
    string? ToolPolicy,
    string? OutputSchema,
    decimal EstimatedCredits);

/// <summary>自建专家创建请求；创建后自动生成 <c>custom-</c> 前缀编码与 v1 已发布版本。</summary>
public sealed class ExpertCreateRequest
{
    /// <summary>专家展示名称。</summary>
    [Required, StringLength(128), Description("专家展示名称，最长 128 字符。")]
    public string Name { get; init; } = null!;

    /// <summary>专家分类。</summary>
    [Required, StringLength(32), Description("专家分类，最长 32 字符。")]
    public string Category { get; init; } = null!;

    /// <summary>专家描述。</summary>
    [StringLength(1000), Description("专家描述，最长 1000 字符。")]
    public string? Description { get; init; }

    /// <summary>角色设定（人设），运行期提示词片段。</summary>
    [Required, StringLength(4000), Description("角色设定（人设），运行期提示词片段。")]
    public string Persona { get; init; } = null!;

    /// <summary>方法论说明，影响思考链风格。</summary>
    [StringLength(2000), Description("方法论说明，影响思考链风格。")]
    public string? Methodology { get; init; }

    /// <summary>完整提示词模板。</summary>
    [Required, StringLength(8000), Description("完整提示词模板，最长 8000 字符。")]
    public string PromptTemplate { get; init; } = null!;

    /// <summary>工具策略 JSON，决定可调用的 Skill / Connector 集合；非法 JSON 返回 422。</summary>
    [Description("工具策略 JSON；非法 JSON 返回 422。")]
    public string? ToolPolicyJson { get; init; }

    /// <summary>Optional output JSON schema. Declaring <c>properties.memoryCandidates</c> opts this Expert into review-only memory proposals.</summary>
    [Description("可选输出 JSON Schema；声明 properties.memoryCandidates 时才启用待审核记忆候选。")]
    public string? OutputSchemaJson { get; init; }

    /// <summary>单次运行的预估积分消耗，默认 1。</summary>
    [Description("单次运行的预估积分消耗，默认 1。")]
    public decimal? EstimatedCredits { get; init; }
}

/// <summary>自建专家更新请求；全量替换头部字段并生成 <c>version+1</c> 已发布版本，携带 RowVersion 乐观锁。</summary>
public sealed class ExpertUpdateRequest
{
    /// <summary>专家展示名称。</summary>
    [Required, StringLength(128), Description("专家展示名称，最长 128 字符。")]
    public string Name { get; init; } = null!;

    /// <summary>专家分类。</summary>
    [Required, StringLength(32), Description("专家分类，最长 32 字符。")]
    public string Category { get; init; } = null!;

    /// <summary>专家描述。</summary>
    [StringLength(1000), Description("专家描述，最长 1000 字符。")]
    public string? Description { get; init; }

    /// <summary>角色设定（人设），运行期提示词片段。</summary>
    [Required, StringLength(4000), Description("角色设定（人设），运行期提示词片段。")]
    public string Persona { get; init; } = null!;

    /// <summary>方法论说明，影响思考链风格。</summary>
    [StringLength(2000), Description("方法论说明，影响思考链风格。")]
    public string? Methodology { get; init; }

    /// <summary>完整提示词模板。</summary>
    [Required, StringLength(8000), Description("完整提示词模板，最长 8000 字符。")]
    public string PromptTemplate { get; init; } = null!;

    /// <summary>工具策略 JSON；非法 JSON 返回 422。</summary>
    [Description("工具策略 JSON；非法 JSON 返回 422。")]
    public string? ToolPolicyJson { get; init; }

    /// <summary>Optional output JSON schema for the next immutable Expert version.</summary>
    [Description("可选输出 JSON Schema；声明 properties.memoryCandidates 时才启用待审核记忆候选。")]
    public string? OutputSchemaJson { get; init; }

    /// <summary>单次运行的预估积分消耗，默认 1。</summary>
    [Description("单次运行的预估积分消耗，默认 1。")]
    public decimal? EstimatedCredits { get; init; }

    /// <summary>乐观锁版本号；与服务端不一致返回 409。</summary>
    [Required, Description("乐观锁版本号，与服务端不一致返回 409/40903。")]
    public long RowVersion { get; init; }
}
