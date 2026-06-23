using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IStudentCredentialEmailSender
{
    Task SendAsync(
        StudentCredentialEmailRequest request,
        CancellationToken cancellationToken = default);
}
