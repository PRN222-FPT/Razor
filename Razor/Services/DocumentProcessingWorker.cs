using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Services;

public sealed class DocumentProcessingWorker(
    IDocumentProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<DocumentProcessingOptions> options,
    ILogger<DocumentProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnqueueQueuedDocumentsAsync(stoppingToken);

        var queueTask = ProcessQueuedDocumentsAsync(stoppingToken);
        var pollingTask = PollQueuedDocumentsAsync(stoppingToken);

        await Task.WhenAll(queueTask, pollingTask);
    }

    private async Task ProcessQueuedDocumentsAsync(CancellationToken stoppingToken)
    {
        await foreach (var documentId in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();

                await processor.ProcessDocumentAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected document processing worker failure. DocumentId={DocumentId}",
                    documentId);
            }
        }
    }

    private async Task PollQueuedDocumentsAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Max(5, options.Value.QueuePollIntervalSeconds)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EnqueueQueuedDocumentsAsync(stoppingToken);
        }
    }

    private async Task EnqueueQueuedDocumentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
            var queuedDocumentIds = await processor.GetQueuedDocumentIdsAsync(cancellationToken);

            foreach (var documentId in queuedDocumentIds)
            {
                queue.Enqueue(documentId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Queued document polling failed.");
        }
    }
}
