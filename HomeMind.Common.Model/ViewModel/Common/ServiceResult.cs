namespace HomeMind.Common.Model.ViewModel.Common;

/// <summary>业务服务的统一执行结果，由控制器转换为 HTTP 响应。</summary>
public sealed record ServiceResult(int StatusCode, string Message, object? Data = null)
{
    public bool Succeeded => StatusCode is >= 200 and < 300;
}
