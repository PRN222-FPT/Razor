using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IEmbeddingService
{
    Task<IReadOnlyList<IReadOnlyList<float>>> EmbedAsync(
        IReadOnlyList<DocumentChunkDraft> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float>> EmbedQueryAsync(
        string query,
        CancellationToken cancellationToken = default);
}
