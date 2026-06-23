using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IAnswerGenerationService
{
    Task<string> GenerateAnswerAsync(
        AnswerGenerationRequest request,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default);
}
