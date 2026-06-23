using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IRagChatService
{
    Task<StudentChatPageDto> GetChatPageAsync(
        Guid userId,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default);

    Task<Guid> StartNewSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<StudentChatAnswerDto> AskAsync(
        StudentChatRequest request,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default);
}
