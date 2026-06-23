using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IAuthService
{
    Task<LoginResult> ValidateCredentialsAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
