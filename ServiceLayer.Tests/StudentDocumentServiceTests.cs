using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class StudentDocumentServiceTests
{
    [Fact]
    public async Task GetDocumentDetailsAsync_ReturnsDocumentWithOrderedChunks()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor"
        });
        context.Chapters.Add(new Chapter
        {
            ChapterId = chapterId,
            SubjectId = subjectId,
            ChapterTitle = "Routing"
        });
        context.Documents.Add(new Document
        {
            DocumentId = documentId,
            SubjectId = subjectId,
            ChapterId = chapterId,
            Title = "Routing Guide",
            FileUrl = "docs/routing.pdf",
            FileType = "pdf",
            Status = "completed",
            CreatedAt = new DateTime(2026, 6, 18, 9, 0, 0)
        });
        context.Chunks.AddRange(
            new Chunk
            {
                ChunkId = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = 1,
                Content = "Second chunk",
                CreatedAt = new DateTime(2026, 6, 18, 9, 10, 0)
            },
            new Chunk
            {
                ChunkId = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = 0,
                Content = "First chunk",
                CreatedAt = new DateTime(2026, 6, 18, 9, 5, 0)
            });
        await context.SaveChangesAsync();

        var service = new StudentDocumentService(new UnitOfWork(context));

        var result = await service.GetDocumentDetailsAsync(documentId);

        Assert.NotNull(result);
        Assert.Equal("Routing Guide", result!.Title);
        Assert.Equal("PRN222", result.SubjectCode);
        Assert.Equal("Routing", result.ChapterTitle);
        Assert.Equal("completed", result.Status);
        Assert.Equal(2, result.Chunks.Count);
        Assert.Equal("First chunk", result.Chunks[0].Content);
        Assert.Equal("Second chunk", result.Chunks[1].Content);
    }

    [Fact]
    public async Task GetDocumentDetailsAsync_ReturnsNullWhenDocumentMissing()
    {
        await using var context = CreateContext();
        var service = new StudentDocumentService(new UnitOfWork(context));

        var result = await service.GetDocumentDetailsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
