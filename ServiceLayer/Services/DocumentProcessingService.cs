using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class DocumentProcessingService(
    IUnitOfWork unitOfWork,
    IDocumentParser parser,
    IDocumentChunker chunker,
    IEmbeddingService embeddingService,
    IVectorStore vectorStore,
    IDocumentProcessingNotifier processingNotifier,
    IOptions<TeacherDocumentUploadOptions> uploadOptions,
    ILogger<DocumentProcessingService> logger) : IDocumentProcessingService
{
    private const string QueuedStatus = "queued";
    private const string ProcessingStatus = "processing";
    private const string CompletedStatus = "completed";
    private const string FailedStatus = "failed";

    public async Task<IReadOnlyList<Guid>> GetQueuedDocumentIdsAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Repository<ProcessingJob>()
            .Query()
            .AsNoTracking()
            .Where(job => job.JobStatus == QueuedStatus)
            .OrderBy(job => job.StartedAt)
            .Select(job => job.DocumentId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await unitOfWork.Repository<Document>()
            .Query()
            .Include(item => item.ProcessingJobs)
            .SingleOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null)
        {
            logger.LogWarning("Queued document was not found. DocumentId={DocumentId}", documentId);
            return;
        }

        var job = document.ProcessingJobs
            .Where(item => item.JobStatus == QueuedStatus)
            .OrderBy(item => item.StartedAt)
            .FirstOrDefault();
        if (job is null)
        {
            return;
        }

        job.JobStatus = ProcessingStatus;
        job.StartedAt = CurrentTimestamp();
        job.ErrorMessage = null;
        document.Status = ProcessingStatus;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await processingNotifier.NotifyAsync(
            CreateNotification(document.DocumentId, document.UploadedTeacher, ProcessingStatus),
            cancellationToken);

        try
        {
            var filePath = ResolveStoredFilePath(document.FileUrl);
            var parsed = await parser.ParseAsync(
                filePath,
                document.FileType ?? Path.GetExtension(filePath),
                cancellationToken);
            var chunkDrafts = chunker.CreateChunks(parsed);
            if (chunkDrafts.Count == 0)
            {
                throw new InvalidOperationException("Document did not contain extractable text.");
            }

            var vectors = await embeddingService.EmbedAsync(chunkDrafts, cancellationToken);
            if (vectors.Count != chunkDrafts.Count)
            {
                throw new InvalidOperationException("Embedding response count did not match chunk count.");
            }

            var embeddedChunks = chunkDrafts
                .Select((chunk, index) => new EmbeddedDocumentChunk(
                    Guid.NewGuid(),
                    document.DocumentId,
                    document.SubjectId,
                    document.ChapterId,
                    document.Title,
                    chunk.ChunkIndex,
                    chunk.Content,
                    vectors[index]))
                .ToList();

            await vectorStore.UpsertAsync(embeddedChunks, cancellationToken);
            await ReplaceChunksAsync(document.DocumentId, embeddedChunks, cancellationToken);

            job.JobStatus = CompletedStatus;
            job.FinishedAt = CurrentTimestamp();
            job.ErrorMessage = null;
            document.Status = CompletedStatus;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await processingNotifier.NotifyAsync(
                CreateNotification(
                    document.DocumentId,
                    document.UploadedTeacher,
                    CompletedStatus,
                    embeddedChunks.Count),
                cancellationToken);

            logger.LogInformation(
                "Document processing completed. DocumentId={DocumentId}, ChunkCount={ChunkCount}",
                document.DocumentId,
                embeddedChunks.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            job.JobStatus = FailedStatus;
            job.FinishedAt = CurrentTimestamp();
            job.ErrorMessage = SanitizeErrorMessage(ex.Message);
            document.Status = FailedStatus;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await processingNotifier.NotifyAsync(
                CreateNotification(
                    document.DocumentId,
                    document.UploadedTeacher,
                    FailedStatus,
                    errorMessage: job.ErrorMessage),
                cancellationToken);

            logger.LogWarning(
                ex,
                "Document processing failed. DocumentId={DocumentId}, JobId={JobId}",
                document.DocumentId,
                job.JobId);
        }
    }

    private async Task ReplaceChunksAsync(
        Guid documentId,
        IReadOnlyList<EmbeddedDocumentChunk> embeddedChunks,
        CancellationToken cancellationToken)
    {
        var chunks = unitOfWork.Repository<Chunk>();
        var existingChunks = await chunks.Query()
            .Where(chunk => chunk.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        foreach (var existingChunk in existingChunks)
        {
            chunks.Delete(existingChunk);
        }

        foreach (var embeddedChunk in embeddedChunks)
        {
            await chunks.AddAsync(
                new Chunk
                {
                    ChunkId = embeddedChunk.ChunkId,
                    DocumentId = embeddedChunk.DocumentId,
                    ChunkIndex = embeddedChunk.ChunkIndex,
                    Content = embeddedChunk.Content,
                    CreatedAt = CurrentTimestamp()
                },
                cancellationToken);
        }
    }

    private string ResolveStoredFilePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Document file path is missing.");
        }

        var storageRoot = Path.GetFullPath(uploadOptions.Value.StorageRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(
            Path.Combine(storageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!candidate.Equals(storageRoot, StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith(
                storageRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Document file path is outside configured storage.");
        }

        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException("Stored document file was not found.");
        }

        return candidate;
    }

    private static string SanitizeErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Document processing failed.";
        }

        var sanitized = message.ReplaceLineEndings(" ").Trim();

        return sanitized.Length <= 500
            ? sanitized
            : sanitized[..500];
    }

    private static DateTime CurrentTimestamp()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    private static DocumentProcessingStatusNotification CreateNotification(
        Guid documentId,
        Guid? teacherId,
        string status,
        int? chunkCount = null,
        string? errorMessage = null)
    {
        return new DocumentProcessingStatusNotification(
            documentId,
            teacherId,
            status,
            FormatStatus(status),
            GetStatusClass(status),
            chunkCount,
            errorMessage);
    }

    private static string FormatStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            CompletedStatus => "Processed",
            ProcessingStatus => "Processing",
            QueuedStatus => "Queued",
            FailedStatus => "Failed",
            _ => "Pending"
        };
    }

    private static string GetStatusClass(string status)
    {
        return status.ToLowerInvariant() switch
        {
            CompletedStatus => "processed",
            FailedStatus => "error",
            _ => "indexing"
        };
    }
}
