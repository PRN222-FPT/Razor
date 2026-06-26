using System.Runtime.CompilerServices;
using System.Text;
using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class TeacherDocumentServiceTests
{
    [Fact]
    public async Task UploadAsync_BlocksWhenChapterAlreadyHasDocument()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var oldDocumentId = Guid.NewGuid();
        var oldFilePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{oldDocumentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(oldFilePath)!);
        await File.WriteAllTextAsync(oldFilePath, "old chapter file");

        SeedTeacher(context, teacherId, subjectId, isHeader: true);
        SeedDocument(
            context,
            oldDocumentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, oldFilePath));
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.UploadAsync(CreateUploadRequest(
            teacherId,
            "teacher@example.com",
            subjectId,
            "replacement.pdf",
            "General"));

        Assert.False(result.Succeeded);
        Assert.Contains("chapter already has a document", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vectorStore.DeletedDocumentIds);
        Assert.Empty(queue.EnqueuedDocumentIds);
        Assert.True(File.Exists(oldFilePath));
        Assert.True(await context.Documents.AnyAsync(document => document.DocumentId == oldDocumentId));
    }

    [Fact]
    public async Task UploadAsync_AllowsDifferentChapterForSameSubjectWhenTeacherIsHeader()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var oldDocumentId = Guid.NewGuid();
        var oldFilePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{oldDocumentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(oldFilePath)!);
        await File.WriteAllTextAsync(oldFilePath, "existing general file");

        SeedTeacher(context, teacherId, subjectId, isHeader: true);
        SeedDocument(
            context,
            oldDocumentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, oldFilePath),
            chapterTitle: "General",
            chapterOrder: 1);
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.UploadAsync(CreateUploadRequest(
            teacherId,
            "teacher@example.com",
            subjectId,
            "chapter-2.pdf",
            "Chapter 2"));

        Assert.True(result.Succeeded);
        Assert.Single(queue.EnqueuedDocumentIds);
        Assert.Equal(result.DocumentId, queue.EnqueuedDocumentIds[0]);
        Assert.Equal(2, await context.Documents.CountAsync(document => document.SubjectId == subjectId));

        var chapters = await context.Chapters
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.ChapterOrder)
            .ToListAsync();
        Assert.Equal(2, chapters.Count);
        Assert.Equal("General", chapters[0].ChapterTitle);
        Assert.Equal(1, chapters[0].ChapterOrder);
        Assert.Equal("Chapter 2", chapters[1].ChapterTitle);
        Assert.Equal(2, chapters[1].ChapterOrder);
    }

    [Fact]
    public async Task UploadAsync_BlocksWhenTeacherIsNotSubjectHeader()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        SeedTeacher(context, teacherId, subjectId, isHeader: false);
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.UploadAsync(CreateUploadRequest(
            teacherId,
            "teacher@example.com",
            subjectId,
            "chapter-1.pdf",
            "General"));

        Assert.False(result.Succeeded);
        Assert.Contains("Only the header teacher", result.ErrorMessage);
        Assert.Empty(queue.EnqueuedDocumentIds);
    }

    [Fact]
    public async Task UploadAsync_DoesNotThrowWhenLegacyDuplicateChaptersExist()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var existingDocumentId = Guid.NewGuid();
        var existingFilePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{existingDocumentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(existingFilePath)!);
        await File.WriteAllTextAsync(existingFilePath, "general file");

        SeedTeacher(context, teacherId, subjectId, isHeader: true);
        SeedDocument(
            context,
            existingDocumentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, existingFilePath),
            chapterTitle: "General",
            chapterOrder: 1);
        context.Chapters.Add(new Chapter
        {
            ChapterId = Guid.NewGuid(),
            SubjectId = subjectId,
            ChapterTitle = " General ",
            ChapterOrder = 2,
            CreatedAt = new DateTime(2026, 6, 26, 8, 0, 0)
        });
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.UploadAsync(CreateUploadRequest(
            teacherId,
            "teacher@example.com",
            subjectId,
            "replacement.pdf",
            "general"));

        Assert.False(result.Succeeded);
        Assert.Contains("chapter already has a document", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(queue.EnqueuedDocumentIds);
    }

    [Fact]
    public async Task UploadAsync_ReturnsCompatibilityMessageWhenDatabaseStillHasLegacySubjectConstraint()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        SeedTeacher(context, teacherId, subjectId, isHeader: true);
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(
            new ThrowingUnitOfWork(
                context,
                new DbUpdateException(
                    "duplicate key value violates unique constraint \"documents_subject_id_key\"",
                    new Exception("documents_subject_id_key"))),
            storageRoot,
            vectorStore,
            queue);

        var result = await service.UploadAsync(CreateUploadRequest(
            teacherId,
            "teacher@example.com",
            subjectId,
            "chapter-1.pdf",
            "1"));

        Assert.False(result.Succeeded);
        Assert.Contains("old one-document-per-subject constraint", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(queue.EnqueuedDocumentIds);
        Assert.Empty(vectorStore.DeletedDocumentIds);
        Assert.Empty(Directory.GetFiles(storageRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DeleteAsync_DeletesOnlySelectedDocumentAndVectors()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        var firstFilePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{firstDocumentId:N}.pdf");
        var secondFilePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{secondDocumentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(firstFilePath)!);
        await File.WriteAllTextAsync(firstFilePath, "general file");
        await File.WriteAllTextAsync(secondFilePath, "chapter 2 file");

        SeedTeacher(context, teacherId, subjectId, isHeader: true);
        SeedDocument(
            context,
            firstDocumentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, firstFilePath),
            chapterTitle: "General",
            chapterOrder: 1);
        SeedDocument(
            context,
            secondDocumentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, secondFilePath),
            chapterTitle: "Chapter 2",
            chapterOrder: 2);
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.DeleteAsync(teacherId, "teacher@example.com", firstDocumentId);

        Assert.True(result.Succeeded);
        Assert.Equal([firstDocumentId], vectorStore.DeletedDocumentIds);
        Assert.Empty(queue.EnqueuedDocumentIds);
        Assert.False(File.Exists(firstFilePath));
        Assert.True(File.Exists(secondFilePath));
        Assert.False(await context.Documents.AnyAsync(document => document.DocumentId == firstDocumentId));
        Assert.True(await context.Documents.AnyAsync(document => document.DocumentId == secondDocumentId));
        Assert.False(await context.Chunks.AnyAsync(chunk => chunk.DocumentId == firstDocumentId));
        Assert.True(await context.Chunks.AnyAsync(chunk => chunk.DocumentId == secondDocumentId));
        Assert.False(await context.ProcessingJobs.AnyAsync(job => job.DocumentId == firstDocumentId));
        Assert.True(await context.ProcessingJobs.AnyAsync(job => job.DocumentId == secondDocumentId));
    }

    [Fact]
    public async Task DeleteAsync_AllowsHeaderTeacherToDeleteDocumentUploadedByAnotherUser()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var filePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{documentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "owner file");

        SeedTeacher(context, teacherId, subjectId, isHeader: true);
        SeedUser(context, uploaderUserId, "Uploader", "uploader@example.com");
        SeedDocument(
            context,
            documentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, filePath),
            uploadedByUserId: uploaderUserId);
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.DeleteAsync(teacherId, "teacher@example.com", documentId);

        Assert.True(result.Succeeded);
        Assert.Equal([documentId], vectorStore.DeletedDocumentIds);
        Assert.False(await context.Documents.AnyAsync(document => document.DocumentId == documentId));
    }

    [Fact]
    public async Task DeleteAsync_BlocksWhenTeacherIsNotSubjectHeader()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var filePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{documentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "owner file");

        SeedTeacher(context, teacherId, subjectId, isHeader: false);
        SeedDocument(
            context,
            documentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, filePath));
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.DeleteAsync(teacherId, "teacher@example.com", documentId);

        Assert.False(result.Succeeded);
        Assert.Contains("Only the header teacher", result.ErrorMessage);
        Assert.Empty(vectorStore.DeletedDocumentIds);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task GetDocumentListAsync_ReturnsUploadedByDetailsAndManageFlag()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var filePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{documentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "owner file");

        SeedTeacher(context, teacherId, subjectId, isHeader: true);
        SeedUser(context, uploaderUserId, "Uploader Name", "uploader@example.com");
        SeedDocument(
            context,
            documentId,
            subjectId,
            teacherId,
            RelativePath(storageRoot, filePath),
            uploadedByUserId: uploaderUserId);
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.GetDocumentListAsync("teacher@example.com");

        Assert.Single(result.Documents);
        var row = result.Documents[0];
        Assert.Equal(documentId, row.DocumentId);
        Assert.Equal("Uploader Name", row.UploadedByName);
        Assert.Equal(uploaderUserId, row.UploadedById);
        Assert.Equal("General", row.ChapterTitle);
        Assert.Equal($"{documentId:N}.pdf", row.FileName);
        Assert.Equal("pdf", row.FileType);
        Assert.True(row.CanManage);
    }

    private static TeacherDocumentService CreateService(
        AppDbContext context,
        string storageRoot,
        IVectorStore vectorStore,
        IDocumentProcessingQueue queue)
    {
        return CreateService(new UnitOfWork(context), storageRoot, vectorStore, queue);
    }

    private static TeacherDocumentService CreateService(
        IUnitOfWork unitOfWork,
        string storageRoot,
        IVectorStore vectorStore,
        IDocumentProcessingQueue queue)
    {
        return new TeacherDocumentService(
            unitOfWork,
            queue,
            vectorStore,
            new NoOpDocumentProcessingNotifier(),
            Options.Create(new TeacherDocumentUploadOptions
            {
                AllowedExtensions = [".pdf", ".docx"],
                MaxFileSizeBytes = 10 * 1024 * 1024,
                StorageRootPath = storageRoot
            }),
            NullLogger<TeacherDocumentService>.Instance);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static UploadTeacherDocumentRequest CreateUploadRequest(
        Guid teacherId,
        string email,
        Guid subjectId,
        string fileName,
        string chapterTitle)
    {
        return new UploadTeacherDocumentRequest(
            teacherId,
            email,
            "Replacement",
            subjectId,
            chapterTitle,
            fileName,
            11,
            new MemoryStream(Encoding.UTF8.GetBytes("new content")));
    }

    private static void SeedTeacher(
        AppDbContext context,
        Guid teacherId,
        Guid subjectId,
        bool isHeader)
    {
        context.Teachers.Add(new Teacher
        {
            TeacherId = teacherId,
            FullName = "Teacher",
            Email = "teacher@example.com"
        });
        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "MATH",
            SubjectName = "Math"
        });
        context.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherSubjectId = Guid.NewGuid(),
            TeacherId = teacherId,
            SubjectId = subjectId,
            IsHeadOfDepartment = isHeader
        });
    }

    private static void SeedUser(
        AppDbContext context,
        Guid userId,
        string fullName,
        string email)
    {
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = fullName,
            Email = email,
            PasswordHash = "hash",
            Role = "teacher"
        });
    }

    private static void SeedDocument(
        AppDbContext context,
        Guid documentId,
        Guid subjectId,
        Guid teacherId,
        string relativePath,
        Guid? uploadedByUserId = null,
        string chapterTitle = "General",
        int chapterOrder = 1)
    {
        var chapter = new Chapter
        {
            ChapterId = Guid.NewGuid(),
            SubjectId = subjectId,
            ChapterTitle = chapterTitle,
            ChapterOrder = chapterOrder
        };

        if (!context.Subjects.Local.Any(subject => subject.SubjectId == subjectId)
            && !context.Subjects.Any(subject => subject.SubjectId == subjectId))
        {
            context.Subjects.Add(new Subject
            {
                SubjectId = subjectId,
                SubjectCode = $"S{context.Subjects.Local.Count + 1}",
                SubjectName = "Subject"
            });
        }

        context.Chapters.Add(chapter);
        context.Documents.Add(new Document
        {
            DocumentId = documentId,
            SubjectId = subjectId,
            ChapterId = chapter.ChapterId,
            Title = "Existing",
            FileUrl = relativePath.Replace('\\', '/'),
            FileType = "pdf",
            UploadedTeacher = teacherId,
            UploadedBy = uploadedByUserId ?? teacherId,
            Status = "completed"
        });
        context.Chunks.Add(new Chunk
        {
            ChunkId = Guid.NewGuid(),
            DocumentId = documentId,
            ChunkIndex = 0,
            Content = "Existing content"
        });
        context.ProcessingJobs.Add(new ProcessingJob
        {
            JobId = Guid.NewGuid(),
            DocumentId = documentId,
            JobStatus = "completed"
        });
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }

    private static string RelativePath(string root, string path)
    {
        return Path.GetRelativePath(root, path);
    }

    private sealed class RecordingVectorStore : IVectorStore
    {
        public List<Guid> DeletedDocumentIds { get; } = [];

        public Task UpsertAsync(
            IReadOnlyList<EmbeddedDocumentChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteByDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            DeletedDocumentIds.Add(documentId);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
            VectorSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>([]);
        }
    }

    private sealed class RecordingDocumentProcessingQueue : IDocumentProcessingQueue
    {
        public List<Guid> EnqueuedDocumentIds { get; } = [];

        public void Enqueue(Guid documentId)
        {
            EnqueuedDocumentIds.Add(documentId);
        }

        public async IAsyncEnumerable<Guid> DequeueAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class NoOpDocumentProcessingNotifier : IDocumentProcessingNotifier
    {
        public Task NotifyAsync(
            DocumentProcessingStatusNotification notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingUnitOfWork(
        AppDbContext context,
        Exception saveException) : IUnitOfWork
    {
        private readonly Dictionary<Type, object> repositories = [];

        public IRepository<TEntity> Repository<TEntity>()
            where TEntity : class
        {
            var entityType = typeof(TEntity);
            if (repositories.TryGetValue(entityType, out var repository))
            {
                return (IRepository<TEntity>)repository;
            }

            var createdRepository = new Repository<TEntity>(context);
            repositories[entityType] = createdRepository;

            return createdRepository;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromException<int>(saveException);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
