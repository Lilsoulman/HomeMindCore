namespace HomeMind.Business.IServices.AI;

/// <summary>大模型调用客户端，面向 OpenAI 兼容的 chat/completions 端点。</summary>
public interface ILLMClient
{
    /// <summary>发起一次补全调用。</summary>
    /// <param name="request">调用参数，含端点、模型、密钥、温度与提示。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>补全结果；失败时 Success 为 false 并携带错误码与消息。</returns>
    Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

/// <summary>LLM 补全调用请求。</summary>
/// <param name="Endpoint">OpenAI 兼容端点地址，如 https://api.deepseek.com/v1。</param>
/// <param name="Model">模型名称。</param>
/// <param name="ApiKey">API 密钥明文。</param>
/// <param name="Temperature">采样温度 0~1。</param>
/// <param name="SystemPrompt">系统提示词。</param>
/// <param name="UserMessage">用户消息。</param>
/// <param name="MaxTokens">单次补全的最大 token 数。</param>
public sealed record LlmRequest(string Endpoint, string Model, string ApiKey, double Temperature, string SystemPrompt, string UserMessage, int MaxTokens = 4096);

/// <summary>LLM 补全结果。</summary>
/// <param name="Content">模型返回的文本内容，失败时为空。</param>
/// <param name="TotalTokens">本次调用的总 token 数，不可用时为 null。</param>
/// <param name="Success">是否成功。</param>
/// <param name="ErrorCode">失败错误码，取值参见 <see cref="LlmErrorCodes"/>。</param>
/// <param name="ErrorMessage">失败的人类可读原因。</param>
public sealed record LlmCompletion(string Content, int? TotalTokens, bool Success, string? ErrorCode, string? ErrorMessage);

/// <summary>LLM 调用错误码常量。</summary>
public static class LlmErrorCodes
{
    /// <summary>用户尚未配置 AI 服务。</summary>
    public const string AiConfigMissing = "ai_config_missing";
    /// <summary>用户已保存 AI 服务但主动禁用了 AI 生成能力。</summary>
    public const string AiConfigDisabled = "ai_config_disabled";
    /// <summary>调用超时。</summary>
    public const string Timeout = "llm_timeout";
    /// <summary>HTTP 层错误（认证失败、限流、服务端错误等）。</summary>
    public const string HttpError = "llm_http_error";
    /// <summary>模型返回空内容或输出无法解析。</summary>
    public const string EmptyResponse = "llm_empty_response";
}
