using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class TeacherDocumentService(
    IUnitOfWork unitOfWork,
    IDocumentProcessingQueue processingQueue,
    IVectorStore vectorStore,
    IDocumentProcessingNotifier processingNotifier,
    IOptions<TeacherDocumentUploadOptions> uploadOptions,
    ILogger<TeacherDocumentService> logger) : ITeacherDocumentService
{
    private const string DefaultChapterTitle = "General";
    private const string PendingStatus = "pending";
    private const string QueuedStatus = "queued";

    public async Task<TeacherDocumentDashboardDto> GetDashboardAsync(
        string teacherEmail,
        CancellationToken cancellationToken = default)
    {
        var teacher = await FindTeacherAsync(teacherEmail, cancellationToken);
        if (teacher is null)
        {
            return EmptyDashboard();
        }

        var allowedSubjectIds = await GetAllowedSubjectIdsAsync(teacher.TeacherId, cancellationToken);
        var managedSubjectIds = await GetManagedSubjectIdsAsync(teacher.TeacherId, cancellationToken);

        var subjects = await unitOfWork.Repository<Subject>()
            .Query()
            .AsNoTracking()
            .Where(subject => managedSubjectIds.Contains(subject.SubjectId))
            .OrderBy(subject => subject.SubjectCode)
            .ThenBy(subject => subject.SubjectName)
            .Select(subject => new TeacherUploadSubjectDto(
                subject.SubjectId,
                subject.SubjectCode,
                subject.SubjectName))
            .ToListAsync(cancellationToken);

        var documentsQuery = BuildAccessibleDocumentsQuery(allowedSubjectIds);

        var documents = await documentsQuery
            .OrderByDescending(document => document.CreatedAt)
            .ThenByDescending(document => document.DocumentId)
            .Take(10)
            .Select(document => new DocumentRowProjection(
                document.DocumentId,
                document.Title,
                document.SubjectId,
                document.Subject.SubjectCode,
                document.Subject.SubjectName,
                document.Chapter.ChapterTitle,
                document.FileUrl,
                document.UploadedBy,
                document.UploadedByNavigation != null ? document.UploadedByNavigation.FullName : "Unknown",
                document.CreatedAt,
                string.IsNullOrWhiteSpace(document.Status) ? PendingStatus : document.Status,
                document.FileType,
                managedSubjectIds.Contains(document.SubjectId)))
            .ToListAsync(cancellationToken);

        var recentDocuments = documents
            .Select(document => new TeacherDocumentRowDto(
                document.DocumentId,
                document.Title,
                document.SubjectId,
                document.SubjectCode,
                document.SubjectName,
                document.ChapterTitle,
                GetFileName(document.FileUrl),
                document.UploadedById,
                document.UploadedByName,
                document.CreatedAt,
                document.Status,
                document.FileType,
                document.CanManage))
            .ToList();

        var totalDocuments = await documentsQuery.CountAsync(cancellationToken);
        var queuedDocuments = await unitOfWork.Repository<ProcessingJob>()
            .Query()
            .AsNoTracking()
            .Where(job => allowedSubjectIds.Contains(job.Document.SubjectId)
                && job.JobStatus == QueuedStatus)
            .CountAsync(cancellationToken);

        var storedFilePaths = await documentsQuery
            .Select(document => document.FileUrl)
            .ToListAsync(cancellationToken);
        var usedStorageBytes = GetStoredFileSizes(storedFilePaths);

        return new TeacherDocumentDashboardDto(
            subjects,
            recentDocuments,
            totalDocuments,
            queuedDocuments,
            usedStorageBytes,
            uploadOptions.Value.MaxFileSizeBytes);
    }

    public async Task<TeacherDocumentListDto> GetDocumentListAsync(
        string teacherEmail,
        CancellationToken cancellationToken = default)
    {
        var teacher = await FindTeacherAsync(teacherEmail, cancellationToken);
        if (teacher is null)
        {
            return new TeacherDocumentListDto([]);
        }

        var allowedSubjectIds = await GetAllowedSubjectIdsAsync(teacher.TeacherId, cancellationToken);
        var managedSubjectIds = await GetManagedSubjectIdsAsync(teacher.TeacherId, cancellationToken);
        var documents = await BuildAccessibleDocumentsQuery(allowedSubjectIds)
            .OrderByDescending(document => document.CreatedAt)
            .ThenByDescending(document => document.DocumentId)
            .Select(document => new DocumentRowProjection(
                document.DocumentId,
                document.Title,
                document.SubjectId,
                document.Subject.SubjectCode,
                document.Subject.SubjectName,
                document.Chapter.ChapterTitle,
                document.FileUrl,
                document.UploadedBy,
                document.UploadedByNavigation != null ? document.UploadedByNavigation.FullName : "Unknown",
                document.CreatedAt,
                string.IsNullOrWhiteSpace(document.Status) ? PendingStatus : document.Status,
                document.FileType,
                managedSubjectIds.Contains(document.SubjectId)))
            .ToListAsync(cancellationToken);

        return new TeacherDocumentListDto(documents
            .Select(document => new TeacherDocumentRowDto(
                document.DocumentId,
                document.Title,
                document.SubjectId,
                document.SubjectCode,
                document.SubjectName,
                document.ChapterTitle,
                GetFileName(document.FileUrl),
                document.UploadedById,
                document.UploadedByName,
                document.CreatedAt,
                document.Status,
                document.FileType,
                document.CanManage))
            .ToList());
    }

    public async Task<TeacherDocumentDetailsDto?> GetDocumentDetailsAsync(
        string teacherEmail,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var teacher = await FindTeacherAsync(teacherEmail, cancellationToken);
        if (teacher is null)
        {
            return null;
        }

        var allowedSubjectIds = await GetAllowedSubjectIdsAsync(teacher.TeacherId, cancellationToken);
        var document = await unitOfWork.Repository<Document>()
            .Query()
            .AsNoTracking()
            .Include(item => item.Subject)
            .Include(item => item.Chapter)
            .Include(item => item.Chunks)
            .Include(item => item.ProcessingJobs)
            .SingleOrDefaultAsync(
                item => item.DocumentId == documentId
                    && allowedSubjectIds.Contains(item.SubjectId),
                cancellationToken);
        if (document is null)
        {
            return null;
        }

        var status = string.IsNullOrWhiteSpace(document.Status)
            ? PendingStatus
            : document.Status;
        var latestJob = document.ProcessingJobs
            .OrderByDescending(job => job.StartedAt)
            .ThenByDescending(job => job.FinishedAt)
            .FirstOrDefault();

        return new TeacherDocumentDetailsDto(
            document.DocumentId,
            document.Title,
            document.Subject.SubjectCode,
            document.Subject.SubjectName,
            document.Chapter.ChapterTitle,
            document.CreatedAt,
            status,
            document.FileType,
            latestJob?.ErrorMessage,
            document.Chunks
                .OrderBy(chunk => chunk.ChunkIndex)
                .Select(chunk => new TeacherDocumentChunkDto(
                    chunk.ChunkId,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.CreatedAt))
                .ToList());
    }

    public async Task<UploadTeacherDocumentResult> UploadAsync(
        UploadTeacherDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = uploadOptions.Value;
        var normalizedEmail = request.UserEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return UploadTeacherDocumentResult.Failure("Authenticated teacher email is missing.");
        }

        if (request.FileLength <= 0)
        {
            return UploadTeacherDocumentResult.Failure("Please choose a non-empty file.");
        }

        if (request.FileLength > options.MaxFileSizeBytes)
        {
            return UploadTeacherDocumentResult.Failure($"File size cannot exceed {FormatBytes(options.MaxFileSizeBytes)}.");
        }

        var extension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        if (!IsAllowedExtension(extension, options.AllowedExtensions))
        {
            return UploadTeacherDocumentResult.Failure("Only PDF and DOCX files are supported.");
        }

        var teacher = await FindTeacherAsync(normalizedEmail, cancellationToken);
        if (teacher is null)
        {
            return UploadTeacherDocumentResult.Failure("Teacher profile was not found for this account.");
        }

        var canManageSubject = await unitOfWork.Repository<TeacherSubject>()
            .Query()
            .AsNoTracking()
            .AnyAsync(
                teacherSubject => teacherSubject.TeacherId == teacher.TeacherId
                    && teacherSubject.SubjectId == request.SubjectId
                    && teacherSubject.IsHeadOfDepartment,
                cancellationToken);
        if (!canManageSubject)
        {
            return UploadTeacherDocumentResult.Failure(
                "Only the header teacher for this subject can upload chapter documents.");
        }

        var subjectExists = await unitOfWork.Repository<Subject>()
            .Query()
            .AsNoTracking()
            .AnyAsync(subject => subject.SubjectId == request.SubjectId, cancellationToken);
        if (!subjectExists)
        {
            return UploadTeacherDocumentResult.Failure("The selected subject does not exist.");
        }

        var chapter = await GetOrCreateChapterAsync(
            request.SubjectId,
            request.ChapterTitle,
            cancellationToken);

        var chapterHasDocument = await unitOfWork.Repository<Document>()
            .Query()
            .AsNoTracking()
            .AnyAsync(document => document.ChapterId == chapter.ChapterId, cancellationToken);
        if (chapterHasDocument)
        {
            return UploadTeacherDocumentResult.Failure(
                "This chapter already has a document. Delete the old chapter document before uploading a new one.");
        }

        var documentId = Guid.NewGuid();
        var relativeFilePath = BuildRelativeFilePath(request.SubjectId, documentId, extension);
        var absoluteFilePath = Path.Combine(options.StorageRootPath, relativeFilePath);
        var fileCreated = false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteFilePath)!);
            await using (var destination = new FileStream(
                absoluteFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await request.Content.CopyToAsync(destination, cancellationToken);
            }
            fileCreated = true;

            var document = new Document
            {
                DocumentId = documentId,
                ChapterId = chapter.ChapterId,
                Title = NormalizeTitle(request.Title, request.OriginalFileName),
                FileUrl = relativeFilePath.Replace('\\', '/'),
                FileType = extension.TrimStart('.'),
                UploadedBy = request.UserId,
                UploadedTeacher = teacher.TeacherId,
                Status = PendingStatus,
                CreatedAt = CurrentTimestamp(),
                SubjectId = request.SubjectId
            };

            var processingJob = new ProcessingJob
            {
                JobId = Guid.NewGuid(),
                DocumentId = documentId,
                JobStatus = QueuedStatus
            };

            await unitOfWork.Repository<Document>().AddAsync(document, cancellationToken);
            await unitOfWork.Repository<ProcessingJob>().AddAsync(processingJob, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            processingQueue.Enqueue(documentId);
            await processingNotifier.NotifyAsync(
                CreateNotification(documentId, teacher.TeacherId, QueuedStatus),
                cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "documents_chapter_id_key"))
        {
            if (fileCreated)
            {
                DeleteFileBestEffort(absoluteFilePath);
            }

            return UploadTeacherDocumentResult.Failure(
                "This chapter already has a document. Delete the old chapter document before uploading a new one.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(
            ex,
            "documents_subject_id_key",
            "idx_documents_subject"))
        {
            if (fileCreated)
            {
                DeleteFileBestEffort(absoluteFilePath);
            }

            logger.LogWarning(
                ex,
                "Teacher document upload hit legacy one-document-per-subject constraint. SubjectId={SubjectId}",
                request.SubjectId);

            return UploadTeacherDocumentResult.Failure(
                "The database still has the old one-document-per-subject constraint. Run DataAccessLayer/Scripts/20260626_allow_multiple_subject_documents_by_chapter.sql, then try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "chapters_subject_normalized_title_key"))
        {
            if (fileCreated)
            {
                DeleteFileBestEffort(absoluteFilePath);
            }

            return UploadTeacherDocumentResult.Failure(
                "This chapter already exists for the selected subject. Delete the old chapter document before uploading a new one.");
        }
        catch
        {
            if (fileCreated)
            {
                DeleteFileBestEffort(absoluteFilePath);
            }

            throw;
        }

        logger.LogInformation(
            "Teacher document upload queued. DocumentId={DocumentId}, TeacherId={TeacherId}, SubjectId={SubjectId}",
            documentId,
            teacher.TeacherId,
            request.SubjectId);

        return UploadTeacherDocumentResult.Success(documentId);
    }

    public async Task<DeleteTeacherDocumentResult> DeleteAsync(
        Guid currentUserId,
        string teacherEmail,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = teacherEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return DeleteTeacherDocumentResult.Failure("Authenticated teacher email is missing.");
        }

        var teacher = await FindTeacherAsync(normalizedEmail, cancellationToken);
        if (teacher is null)
        {
            return DeleteTeacherDocumentResult.Failure("Teacher profile was not found for this account.");
        }

        var allowedSubjectIds = await GetAllowedSubjectIdsAsync(teacher.TeacherId, cancellationToken);
        var managedSubjectIds = await GetManagedSubjectIdsAsync(teacher.TeacherId, cancellationToken);
        var document = await unitOfWork.Repository<Document>()
            .Query()
            .Include(item => item.Chunks)
            .Include(item => item.ProcessingJobs)
            .SingleOrDefaultAsync(
                item => item.DocumentId == documentId
                    && allowedSubjectIds.Contains(item.SubjectId),
                cancellationToken);
        if (document is null)
        {
            return DeleteTeacherDocumentResult.Failure("Document was not found or you do not have permission to delete it.");
        }

        if (!managedSubjectIds.Contains(document.SubjectId))
        {
            return DeleteTeacherDocumentResult.Failure(
                "Only the header teacher for this subject can delete chapter documents.");
        }

        var filePath = TryResolveStoredFilePath(uploadOptions.Value.StorageRootPath, document.FileUrl);
        await vectorStore.DeleteByDocumentAsync(document.DocumentId, cancellationToken);
        DeleteExistingSubjectDocuments([document]);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (filePath is not null)
        {
            DeleteFileBestEffort(filePath);
        }

        logger.LogInformation(
            "Teacher document deleted. DocumentId={DocumentId}, TeacherId={TeacherId}, SubjectId={SubjectId}",
            documentId,
            teacher.TeacherId,
            document.SubjectId);

        return DeleteTeacherDocumentResult.Success();
    }

    private void DeleteExistingSubjectDocuments(IReadOnlyList<Document> existingDocuments)
    {
        var jobs = unitOfWork.Repository<ProcessingJob>();
        var chunks = unitOfWork.Repository<Chunk>();
        var documents = unitOfWork.Repository<Document>();

        foreach (var document in existingDocuments)
        {
            foreach (var job in document.ProcessingJobs.ToList())
            {
                jobs.Delete(job);
            }

            foreach (var chunk in document.Chunks.ToList())
            {
                chunks.Delete(chunk);
            }

            documents.Delete(document);
        }
    }

    private async Task<Teacher?> FindTeacherAsync(
        string teacherEmail,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = teacherEmail.Trim().ToLowerInvariant();

        return await unitOfWork.Repository<Teacher>()
            .Query()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                teacher => teacher.Email != null && teacher.Email.ToLower() == normalizedEmail,
                cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> GetAllowedSubjectIdsAsync(
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.Repository<TeacherSubject>()
            .Query()
            .AsNoTracking()
            .Where(teacherSubject => teacherSubject.TeacherId == teacherId)
            .Select(teacherSubject => teacherSubject.SubjectId)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> GetManagedSubjectIdsAsync(
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.Repository<TeacherSubject>()
            .Query()
            .AsNoTracking()
            .Where(teacherSubject => teacherSubject.TeacherId == teacherId
                && teacherSubject.IsHeadOfDepartment)
            .Select(teacherSubject => teacherSubject.SubjectId)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Document> BuildAccessibleDocumentsQuery(IReadOnlyList<Guid> allowedSubjectIds)
    {
        return unitOfWork.Repository<Document>()
            .Query()
            .AsNoTracking()
            .Include(document => document.Subject)
            .Include(document => document.Chapter)
            .Include(document => document.UploadedByNavigation)
            .Where(document => allowedSubjectIds.Contains(document.SubjectId));
    }

    private async Task<Chapter> GetOrCreateChapterAsync(
        Guid subjectId,
        string? requestedTitle,
        CancellationToken cancellationToken)
    {
        var chapterTitle = string.IsNullOrWhiteSpace(requestedTitle)
            ? DefaultChapterTitle
            : requestedTitle.Trim();

        var chapters = unitOfWork.Repository<Chapter>();
        var existingChapter = await chapters.Query()
            .Where(chapter => chapter.SubjectId == subjectId
                && chapter.ChapterTitle.Trim().ToLower() == chapterTitle.ToLower())
            .OrderBy(chapter => chapter.ChapterOrder ?? int.MaxValue)
            .ThenBy(chapter => chapter.CreatedAt)
            .ThenBy(chapter => chapter.ChapterId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingChapter is not null)
        {
            return existingChapter;
        }

        var nextChapterOrder = await chapters.Query()
            .Where(chapter => chapter.SubjectId == subjectId)
            .MaxAsync(chapter => (int?)chapter.ChapterOrder, cancellationToken) ?? 0;

        var chapter = new Chapter
        {
            ChapterId = Guid.NewGuid(),
            SubjectId = subjectId,
            ChapterTitle = chapterTitle,
            ChapterOrder = nextChapterOrder + 1,
            CreatedAt = CurrentTimestamp()
        };

        await chapters.AddAsync(chapter, cancellationToken);

        return chapter;
    }

    private long GetStoredFileSizes(IEnumerable<string> relativePaths)
    {
        var total = 0L;
        foreach (var relativePath in relativePaths)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var path = Path.Combine(
                uploadOptions.Value.StorageRootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                total += new FileInfo(path).Length;
            }
        }

        return total;
    }

    private static bool IsAllowedExtension(string extension, IEnumerable<string> allowedExtensions)
    {
        return allowedExtensions.Any(
            allowed => string.Equals(
                NormalizeExtension(allowed),
                extension,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim();

        return normalized.StartsWith('.')
            ? normalized.ToLowerInvariant()
            : $".{normalized.ToLowerInvariant()}";
    }

    private static string NormalizeTitle(string? title, string originalFileName)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : title.Trim();

        return normalizedTitle.Length <= 255
            ? normalizedTitle
            : normalizedTitle[..255];
    }

    private static string BuildRelativeFilePath(Guid subjectId, Guid documentId, string extension)
    {
        return Path.Combine(subjectId.ToString("N"), $"{documentId:N}{extension}");
    }

    private static string FormatBytes(long bytes)
    {
        var megabytes = bytes / 1024d / 1024d;

        return $"{megabytes:N0} MB";
    }

    private static DateTime CurrentTimestamp()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception,
        params string[] constraintNames)
    {
        if (exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return constraintNames.Any(
                constraintName => string.Equals(
                    postgresException.ConstraintName,
                    constraintName,
                    StringComparison.OrdinalIgnoreCase));
        }

        var errorText = exception.ToString();

        return constraintNames.Any(
            constraintName => errorText.Contains(constraintName, StringComparison.OrdinalIgnoreCase));
    }

    private static TeacherDocumentDashboardDto EmptyDashboard()
    {
        return new TeacherDocumentDashboardDto([], [], 0, 0, 0, 0);
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
            "completed" => "Processed",
            "processing" => "Processing",
            "queued" => "Queued",
            "pending" => "Pending",
            "failed" => "Failed",
            _ => "Pending"
        };
    }

    private static string GetStatusClass(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "completed" => "processed",
            "failed" => "error",
            _ => "indexing"
        };
    }

    private static void DeleteFileBestEffort(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup after a failed database commit.
        }
    }

    private static string? TryResolveStoredFilePath(string storageRootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(storageRootPath) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var storageRoot = Path.GetFullPath(storageRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(
            Path.Combine(storageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (candidate.Equals(storageRoot, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(
                storageRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        return null;
    }

    private static string GetFileName(string fileUrl)
    {
        return string.IsNullOrWhiteSpace(fileUrl)
            ? string.Empty
            : Path.GetFileName(fileUrl);
    }

    private sealed record DocumentRowProjection(
        Guid DocumentId,
        string Title,
        Guid SubjectId,
        string SubjectCode,
        string SubjectName,
        string ChapterTitle,
        string FileUrl,
        Guid? UploadedById,
        string UploadedByName,
        DateTime? CreatedAt,
        string Status,
        string? FileType,
        bool CanManage);
}
