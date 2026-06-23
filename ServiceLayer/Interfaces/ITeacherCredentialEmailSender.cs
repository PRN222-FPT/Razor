using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface ITeacherCredentialEmailSender
{
    Task SendAsync(
        TeacherCredentialEmailRequest request,
        CancellationToken cancellationToken = default);
}
