using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMind.Business.IServices.AI;

namespace HomeMind.Business.Services.AI;

/// <summary>OpenAI 兼容 chat/completions 客户端，超时 90 秒，失败映射为错误码。</summary>
public sealed class OpenAICompatibleLlmClient : ILLMClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _http;

    public OpenAICompatibleLlmClient(IHttpClientFactory http) => _http = http;

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) || string.IsNullOrWhiteSpace(request.Model) || string.IsNullOrWhiteSpace(request.ApiKey))
            return new LlmCompletion("", null, false, LlmErrorCodes.AiConfigMissing, "AI 配置不完整。");

        using var client = _http.CreateClient("llm");
        client.Timeout = Timeout;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);

        var body = new
        {
            model = request.Model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserMessage }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        try
        {
            using var response = await client.PostAsJsonAsync($"{request.Endpoint.TrimEnd('/')}/chat/completions", body, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new LlmCompletion("", null, false, LlmErrorCodes.HttpError, $"模型服务返回 HTTP {(int)response.StatusCode}。");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var content = root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
                          && choices[0].TryGetProperty("message", out var message)
                          && message.TryGetProperty("content", out var text)
                ? text.GetString() ?? ""
                : "";
            var tokens = root.TryGetProperty("usage", out var usage) && usage.TryGetProperty("total_tokens", out var total)
                ? total.GetInt32()
                : (int?)null;
            if (string.IsNullOrWhiteSpace(content))
                return new LlmCompletion("", tokens, false, LlmErrorCodes.EmptyResponse, "模型未返回有效内容。");
            return new LlmCompletion(content, tokens, true, null, null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LlmCompletion("", null, false, LlmErrorCodes.Timeout, "模型调用超时。");
        }
        catch (Exception error)
        {
            return new LlmCompletion("", null, false, LlmErrorCodes.HttpError, $"模型调用失败：{error.Message}");
        }
    }
}
