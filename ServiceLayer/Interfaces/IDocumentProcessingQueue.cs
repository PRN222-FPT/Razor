namespace ServiceLayer.Interfaces;

public interface IDocumentProcessingQueue
{
    void Enqueue(Guid documentId);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken = default);
}
