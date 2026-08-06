namespace HomeMind.Common.Model.ViewModel.Data.AI;

/// <summary>保存或更新 AI 配置的请求参数。</summary>
/// <param name="Endpoint">OpenAI 兼容的 API 端点地址，如 https://api.openai.com/v1。</param>
/// <param name="Model">默认使用的模型名称。</param>
/// <param name="Temperature">生成温度参数，取值范围 0~1。</param>
/// <param name="ApiKey">API 密钥，可空表示不修改已保存的密钥。</param>
public sealed record AiConfigRequest(string Endpoint, string Model, double Temperature, string? ApiKey);
