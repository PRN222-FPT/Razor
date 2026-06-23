namespace ServiceLayer.Interfaces;

public interface IDocumentProcessingService
{
    Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetQueuedDocumentIdsAsync(CancellationToken cancellationToken = default);
}
