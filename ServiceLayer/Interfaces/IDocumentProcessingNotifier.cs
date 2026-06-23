using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IDocumentProcessingNotifier
{
    Task NotifyAsync(
        DocumentProcessingStatusNotification notification,
        CancellationToken cancellationToken = default);
}
