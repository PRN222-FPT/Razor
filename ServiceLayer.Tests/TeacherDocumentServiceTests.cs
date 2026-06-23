using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DataAccessLayer;
using DataAccessLayer.Entities;
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
    public async Task UploadAsync_BlocksWhenSubjectAlreadyHasDocumentation()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var oldDocumentId = Guid.NewGuid();
        var oldFilePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{oldDocumentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(oldFilePath)!);
        await File.WriteAllTextAsync(oldFilePath, "old subject file");

        SeedTeacher(context, teacherId, subjectId);
        SeedDocument(context, oldDocumentId, subjectId, teacherId, RelativePath(storageRoot, oldFilePath));
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.UploadAsync(CreateUploadRequest(
            teacherId,
            "teacher@example.com",
            subjectId,
            "replacement.pdf"));

        Assert.False(result.Succeeded);
        Assert.Contains("already has a document", result.ErrorMessage);
        Assert.Empty(vectorStore.DeletedSubjectIds);
        Assert.Empty(queue.EnqueuedDocumentIds);
        Assert.True(File.Exists(oldFilePath));
        Assert.True(await context.Documents.AnyAsync(document => document.DocumentId == oldDocumentId));
        Assert.True(await context.Chunks.AnyAsync(chunk => chunk.DocumentId == oldDocumentId));
        Assert.True(await context.ProcessingJobs.AnyAsync(job => job.DocumentId == oldDocumentId));
    }

    [Fact]
    public async Task DeleteAsync_DeletesAssignedSubjectDocumentation()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var filePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{documentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "old subject file");

        SeedTeacher(context, teacherId, subjectId);
        SeedDocument(context, documentId, subjectId, teacherId, RelativePath(storageRoot, filePath));
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.DeleteAsync(teacherId, "teacher@example.com", documentId);

        Assert.True(result.Succeeded);
        Assert.Equal([subjectId], vectorStore.DeletedSubjectIds);
        Assert.Empty(queue.EnqueuedDocumentIds);
        Assert.False(File.Exists(filePath));
        Assert.False(await context.Documents.AnyAsync(document => document.DocumentId == documentId));
        Assert.False(await context.Chunks.AnyAsync(chunk => chunk.DocumentId == documentId));
        Assert.False(await context.ProcessingJobs.AnyAsync(job => job.DocumentId == documentId));
    }

    [Fact]
    public async Task DeleteAsync_BlocksWhenCurrentUserDidNotUploadDocument()
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

        SeedTeacher(context, teacherId, subjectId);
        SeedUser(context, uploaderUserId, "Uploader", "uploader@example.com");
        SeedDocument(context, documentId, subjectId, teacherId, RelativePath(storageRoot, filePath), uploaderUserId);
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.DeleteAsync(teacherId, "teacher@example.com", documentId);

        Assert.False(result.Succeeded);
        Assert.Contains("Only the teacher who uploaded this document can delete it", result.ErrorMessage);
        Assert.Empty(vectorStore.DeletedSubjectIds);
        Assert.True(File.Exists(filePath));
        Assert.True(await context.Documents.AnyAsync(document => document.DocumentId == documentId));
    }

    [Fact]
    public async Task UploadAsync_DoesNotDeleteExistingDocumentation_WhenTeacherCannotUploadSubject()
    {
        var storageRoot = CreateTempDirectory();
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var oldDocumentId = Guid.NewGuid();
        var oldFilePath = Path.Combine(storageRoot, subjectId.ToString("N"), $"{oldDocumentId:N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(oldFilePath)!);
        await File.WriteAllTextAsync(oldFilePath, "old subject file");

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
        SeedDocument(context, oldDocumentId, subjectId, teacherId, RelativePath(storageRoot, oldFilePath));
        await context.SaveChangesAsync();

        var vectorStore = new RecordingVectorStore();
        var queue = new RecordingDocumentProcessingQueue();
        var service = CreateService(context, storageRoot, vectorStore, queue);

        var result = await service.UploadAsync(CreateUploadRequest(
            teacherId,
            "teacher@example.com",
            subjectId,
            "replacement.pdf"));

        Assert.False(result.Succeeded);
        Assert.Empty(vectorStore.DeletedSubjectIds);
        Assert.Empty(queue.EnqueuedDocumentIds);
        Assert.True(File.Exists(oldFilePath));
        Assert.True(await context.Documents.AnyAsync(document => document.DocumentId == oldDocumentId));
    }

    [Fact]
    public async Task GetDocumentListAsync_ReturnsUploadedByDetails()
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

        SeedTeacher(context, teacherId, subjectId);
        SeedUser(context, uploaderUserId, "Uploader Name", "uploader@example.com");
        SeedDocument(context, documentId, subjectId, teacherId, RelativePath(storageRoot, filePath), uploaderUserId);
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
    }

    private static TeacherDocumentService CreateService(
        AppDbContext context,
        string storageRoot,
        IVectorStore vectorStore,
        IDocumentProcessingQueue queue)
    {
        return new TeacherDocumentService(
            new UnitOfWork(context),
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
        string fileName)
    {
        return new UploadTeacherDocumentRequest(
            teacherId,
            email,
            "Replacement",
            subjectId,
            "General",
            fileName,
            11,
            new MemoryStream(Encoding.UTF8.GetBytes("new content")));
    }

    private static void SeedTeacher(AppDbContext context, Guid teacherId, Guid subjectId)
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
            SubjectId = subjectId
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
        Guid? uploadedByUserId = null)
    {
        var chapter = new Chapter
        {
            ChapterId = Guid.NewGuid(),
            SubjectId = subjectId,
            ChapterTitle = "General",
            ChapterOrder = 1
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
        public List<Guid> DeletedSubjectIds { get; } = [];

        public Task UpsertAsync(
            IReadOnlyList<EmbeddedDocumentChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteBySubjectAsync(Guid subjectId, CancellationToken cancellationToken = default)
        {
            DeletedSubjectIds.Add(subjectId);

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
}
