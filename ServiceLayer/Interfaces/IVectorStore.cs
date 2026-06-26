using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IVectorStore
{
    Task UpsertAsync(
        IReadOnlyList<EmbeddedDocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    Task DeleteByDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}
