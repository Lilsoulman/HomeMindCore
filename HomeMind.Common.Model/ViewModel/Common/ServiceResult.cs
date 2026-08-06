namespace HomeMind.Common.Model.ViewModel.Common;

/// <summary>业务服务的统一执行结果，由控制器转换为 HTTP 响应。</summary>
/// <param name="StatusCode">HTTP 语义状态码。</param>
/// <param name="Message">人类可读的结果消息。</param>
/// <param name="Data">业务数据，可为空。</param>
/// <param name="ErrorCode">应用层业务错误码，缺省时由 HTTP 状态码推导。</param>
public sealed record ServiceResult(int StatusCode, string Message, object? Data = null, int? ErrorCode = null)
{
    /// <summary>是否成功（HTTP 2xx）。</summary>
    public bool Succeeded => StatusCode is >= 200 and < 300;
    /// <summary>最终对外暴露的业务错误码。</summary>
    public int Code => Succeeded ? ApiErrorCodes.Success : ErrorCode ?? ApiErrorCodes.FromHttpStatus(StatusCode);
}
