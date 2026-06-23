using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IAccountSecurityService
{
    Task<ChangePasswordResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<PasswordResetRequestResult> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<ResetPasswordResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task ClearPasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken = default);
}
