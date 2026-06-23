using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IPasswordResetEmailSender
{
    Task SendAsync(
        PasswordResetEmailRequest request,
        CancellationToken cancellationToken = default);
}
