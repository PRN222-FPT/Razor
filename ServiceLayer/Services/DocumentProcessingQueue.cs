using System.Threading.Channels;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(Guid documentId)
    {
        if (documentId == Guid.Empty)
        {
            return;
        }

        _queue.Writer.TryWrite(documentId);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}
