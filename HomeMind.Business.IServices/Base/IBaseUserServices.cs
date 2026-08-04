using HomeMind.Common.Model.ViewModel.Data.Base;

namespace HomeMind.Business.IServices.Base;

/// <summary>账户、登录标识和会话的业务服务约定。</summary>
public interface IBaseUserServices
{
    Task<AuthenticationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<BaseUserViewModel?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default);
}
