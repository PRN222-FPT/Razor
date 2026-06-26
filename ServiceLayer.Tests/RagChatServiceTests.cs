using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Services;
using System.Text.Json;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class RagChatServiceTests
{
    [Fact]
    public async Task StartNewSessionAsync_CreatesEmptySessionForUser()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Student",
            Email = "student@example.com",
            PasswordHash = "hashed",
            Role = "student"
        });
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            new RecordingEmbeddingService(),
            new RecordingVectorStore(),
            new RecordingAnswerService("unused"));

        var sessionId = await service.StartNewSessionAsync(userId);

        Assert.NotEqual(Guid.Empty, sessionId);
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.Equal(0, await context.Messages.CountAsync());

        var session = await context.Sessions.SingleAsync();
        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal(userId, session.UserId);
        Assert.Null(session.SubjectId);
        Assert.NotNull(session.StartedAt);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public async Task AskAsync_SearchesSubjectContext_GeneratesAnswerAndStoresMessages()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        SeedChatData(context, userId, subjectId, chapterId, documentId, chunkId);
        await context.SaveChangesAsync();
        var embedding = new RecordingEmbeddingService();
        var vectorStore = new RecordingVectorStore(
            new VectorSearchResult(
                chunkId,
                documentId,
                subjectId,
                chapterId,
                "OOP Lecture",
                0,
                0.91));
        var answer = new RecordingAnswerService("Polymorphism lets objects share an interface with different behavior.");
        var service = CreateService(context, embedding, vectorStore, answer);
        var deltas = new List<string>();

        var result = await service.AskAsync(
            new StudentChatRequest(userId, null, subjectId, "What is polymorphism?"),
            delta =>
            {
                deltas.Add(delta);
                return Task.CompletedTask;
            });

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.Equal("What is polymorphism?", embedding.Query);
        Assert.Equal(subjectId, vectorStore.Request?.SubjectId);
        Assert.Equal(4, vectorStore.Request?.Limit);
        Assert.Single(answer.Request!.Contexts);
        Assert.Equal("Polymorphism lets objects share an interface with different behavior.", result.Answer);
        Assert.Equal(["Polymorphism lets objects share an interface with different behavior."], deltas);
        Assert.Single(result.Citations);
        Assert.Equal(2, await context.Messages.CountAsync());
        Assert.Equal(subjectId, (await context.Sessions.SingleAsync()).SubjectId);
        var messages = await context.Messages.ToListAsync();
        Assert.Contains(messages, message => message.SenderRole == "student");
        Assert.Contains(messages, message => message.SenderRole == "assistant" && message.CitationsJson is not null);
        var assistantMessage = Assert.Single(messages, message => message.SenderRole == "assistant");
        var persistedCitations = JsonSerializer.Deserialize<List<StudentChatCitationDto>>(assistantMessage.CitationsJson!);
        Assert.NotNull(persistedCitations);
        Assert.Single(persistedCitations!);
    }

    [Fact]
    public async Task AskAsync_ReturnsFallback_WhenSearchHasNoContexts()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Student",
            Email = "student@example.com",
            PasswordHash = "hashed",
            Role = "student"
        });
        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor"
        });
        await context.SaveChangesAsync();
        var answer = new RecordingAnswerService("Không tìm thấy nội dung liên quan trong tài liệu đã tải lên cho môn học này.");
        var service = CreateService(
            context,
            new RecordingEmbeddingService(),
            new RecordingVectorStore(),
            answer);
        var deltas = new List<string>();

        var result = await service.AskAsync(
            new StudentChatRequest(userId, null, subjectId, "Unknown topic?"),
            delta =>
            {
                deltas.Add(delta);
                return Task.CompletedTask;
            });

        Assert.Empty(result.Citations);
        Assert.Empty(answer.Request!.Contexts);
        Assert.Equal(["Không tìm thấy nội dung liên quan trong tài liệu đã tải lên cho môn học này."], deltas);
        Assert.Equal(2, await context.Messages.CountAsync());
        var assistantMessage = await context.Messages.SingleAsync(message => message.SenderRole == "assistant");
        Assert.Equal("[]", assistantMessage.CitationsJson);
    }

    [Fact]
    public async Task GetChatPageAsync_ReturnsDocumentsGroupedBySubject()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var subjectWithDocumentId = Guid.NewGuid();
        var subjectWithoutDocumentId = Guid.NewGuid();
        var firstChapterId = Guid.NewGuid();
        var secondChapterId = Guid.NewGuid();
        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();

        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Student",
            Email = "student@example.com",
            PasswordHash = "hashed",
            Role = "student"
        });
        context.Subjects.AddRange(
            new Subject
            {
                SubjectId = subjectWithDocumentId,
                SubjectCode = "PRN222",
                SubjectName = "Razor"
            },
            new Subject
            {
                SubjectId = subjectWithoutDocumentId,
                SubjectCode = "SWD392",
                SubjectName = "Project"
            });
        context.Chapters.Add(new Chapter
        {
            ChapterId = firstChapterId,
            SubjectId = subjectWithDocumentId,
            ChapterTitle = "Dependency Injection"
        });
        context.Chapters.Add(new Chapter
        {
            ChapterId = secondChapterId,
            SubjectId = subjectWithDocumentId,
            ChapterTitle = "Middleware"
        });
        context.Documents.AddRange(
            new Document
            {
                DocumentId = firstDocumentId,
                SubjectId = subjectWithDocumentId,
                ChapterId = firstChapterId,
                Title = "Week 1 Slides",
                FileUrl = "week1.pdf",
                FileType = "pdf",
                Status = "completed",
                CreatedAt = new DateTime(2026, 6, 18, 10, 30, 0)
            },
            new Document
            {
                DocumentId = secondDocumentId,
                SubjectId = subjectWithDocumentId,
                ChapterId = secondChapterId,
                Title = "Week 2 Slides",
                FileUrl = "week2.pdf",
                FileType = "pdf",
                Status = "completed",
                CreatedAt = new DateTime(2026, 6, 19, 9, 0, 0)
            });
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            new RecordingEmbeddingService(),
            new RecordingVectorStore(),
            new RecordingAnswerService("unused"));

        var page = await service.GetChatPageAsync(userId);

        Assert.Equal(2, page.Subjects.Count);
        Assert.Equal(2, page.DocumentLibrary.TotalDocuments);
        Assert.Equal(2, page.DocumentLibrary.Subjects.Count);

        var subjectWithDocument = Assert.Single(
            page.DocumentLibrary.Subjects,
            subject => subject.SubjectId == subjectWithDocumentId);
        Assert.Equal(2, subjectWithDocument.Documents.Count);
        Assert.Equal("Week 2 Slides", subjectWithDocument.Documents[0].Title);
        Assert.Equal("Middleware", subjectWithDocument.Documents[0].ChapterTitle);
        Assert.Equal("Week 1 Slides", subjectWithDocument.Documents[1].Title);
        Assert.Equal("Dependency Injection", subjectWithDocument.Documents[1].ChapterTitle);
        Assert.All(subjectWithDocument.Documents, document => Assert.Equal("completed", document.Status));

        var subjectWithoutDocument = Assert.Single(
            page.DocumentLibrary.Subjects,
            subject => subject.SubjectId == subjectWithoutDocumentId);
        Assert.Empty(subjectWithoutDocument.Documents);
    }

    [Fact]
    public async Task GetChatPageAsync_ReturnsSelectedSessionAndHistory()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var olderSessionId = Guid.NewGuid();
        var newerSessionId = Guid.NewGuid();
        var olderCreatedAt = new DateTime(2026, 6, 18, 8, 0, 0);
        var newerCreatedAt = new DateTime(2026, 6, 18, 9, 0, 0);

        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Student",
            Email = "student@example.com",
            PasswordHash = "hashed",
            Role = "student"
        });
        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor"
        });
        context.Sessions.AddRange(
            new Session
            {
                SessionId = olderSessionId,
                UserId = userId,
                SubjectId = subjectId,
                StartedAt = olderCreatedAt
            },
            new Session
            {
                SessionId = newerSessionId,
                UserId = userId,
                SubjectId = subjectId,
                StartedAt = newerCreatedAt
            });
        context.Messages.AddRange(
            new Message
            {
                MessageId = Guid.NewGuid(),
                SessionId = olderSessionId,
                SenderRole = "student",
                MessageContent = "How do I start?",
                CreatedAt = olderCreatedAt
            },
            new Message
            {
                MessageId = Guid.NewGuid(),
                SessionId = olderSessionId,
                SenderRole = "assistant",
                MessageContent = "Start with the assignment brief.",
                CreatedAt = olderCreatedAt.AddMinutes(1),
                CitationsJson = JsonSerializer.Serialize(
                    new[]
                    {
                        new StudentChatCitationDto(
                            Guid.NewGuid(),
                            "Week 1 Slides",
                            "PRN222",
                            "Dependency Injection",
                            3,
                            "Use the assignment brief to identify the project boundaries.",
                            0.87)
                    })
            },
            new Message
            {
                MessageId = Guid.NewGuid(),
                SessionId = newerSessionId,
                SenderRole = "student",
                MessageContent = "What is DI?",
                CreatedAt = newerCreatedAt
            });
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            new RecordingEmbeddingService(),
            new RecordingVectorStore(),
            new RecordingAnswerService("unused"));

        var page = await service.GetChatPageAsync(userId, olderSessionId);

        Assert.Equal(olderSessionId, page.SessionId);
        Assert.Equal(subjectId, page.SubjectId);
        Assert.Equal(2, page.Messages.Count);
        Assert.Equal("How do I start?", page.Messages[0].MessageContent);
        Assert.Empty(page.Messages[0].Citations);
        Assert.Single(page.Messages[1].Citations);
        Assert.Equal("Week 1 Slides", page.Messages[1].Citations[0].DocumentTitle);
        Assert.NotNull(page.Sessions);
        var sessions = page.Sessions!;
        Assert.Equal(2, sessions.Count);
        Assert.Equal(newerSessionId, sessions[0].SessionId);
        Assert.Equal("What is DI?", sessions[0].Title);
        Assert.Equal("What is DI?", sessions[0].Preview);
        Assert.Equal(1, sessions[0].MessageCount);
        Assert.Equal(olderSessionId, sessions[1].SessionId);
        Assert.Equal("How do I start?", sessions[1].Title);
        Assert.Equal("Start with the assignment brief.", sessions[1].Preview);
        Assert.Equal(2, sessions[1].MessageCount);
    }

    [Fact]
    public async Task GetChatPageAsync_TruncatesLongSessionTitle()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Student",
            Email = "student@example.com",
            PasswordHash = "hashed",
            Role = "student"
        });
        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor"
        });
        context.Sessions.Add(new Session
        {
            SessionId = sessionId,
            UserId = userId,
            SubjectId = subjectId,
            StartedAt = new DateTime(2026, 6, 18, 8, 0, 0)
        });
        context.Messages.AddRange(
            new Message
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                SenderRole = "student",
                MessageContent = "How does dependency injection work in ASP.NET Core when services are scoped, singleton, and transient across the application pipeline?",
                CreatedAt = new DateTime(2026, 6, 18, 8, 0, 0)
            },
            new Message
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                SenderRole = "assistant",
                MessageContent = "It resolves services from the container based on lifetime.",
                CreatedAt = new DateTime(2026, 6, 18, 8, 1, 0)
            });
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            new RecordingEmbeddingService(),
            new RecordingVectorStore(),
            new RecordingAnswerService("unused"));

        var page = await service.GetChatPageAsync(userId, sessionId);

        var session = Assert.Single(page.Sessions!);
        Assert.True(session.Title.Length <= 48);
        Assert.StartsWith("How does dependency injection work in ASP.NET", session.Title);
        Assert.EndsWith("...", session.Title);
    }

    private static RagChatService CreateService(
        AppDbContext context,
        IEmbeddingService embedding,
        IVectorStore vectorStore,
        IAnswerGenerationService answer)
    {
        return new RagChatService(
            new UnitOfWork(context),
            embedding,
            vectorStore,
            answer,
            Options.Create(new RagChatOptions
            {
                SearchLimit = 4,
                MinimumScore = 0.2,
                MaxQuestionLength = 1000,
                MaxContextCharacters = 6000
            }),
            NullLogger<RagChatService>.Instance);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedChatData(
        AppDbContext context,
        Guid userId,
        Guid subjectId,
        Guid chapterId,
        Guid documentId,
        Guid chunkId)
    {
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Student",
            Email = "student@example.com",
            PasswordHash = "hashed",
            Role = "student"
        });
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
            ChapterTitle = "OOP"
        });
        context.Documents.Add(new Document
        {
            DocumentId = documentId,
            SubjectId = subjectId,
            ChapterId = chapterId,
            Title = "OOP Lecture",
            FileUrl = "oop.pdf",
            Status = "completed"
        });
        context.Chunks.Add(new Chunk
        {
            ChunkId = chunkId,
            DocumentId = documentId,
            ChunkIndex = 0,
            Content = "Polymorphism allows one interface to represent different concrete behaviors."
        });
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public string? Query { get; private set; }

        public Task<IReadOnlyList<IReadOnlyList<float>>> EmbedAsync(
            IReadOnlyList<DocumentChunkDraft> chunks,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([]);
        }

        public Task<IReadOnlyList<float>> EmbedQueryAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Query = query;

            return Task.FromResult<IReadOnlyList<float>>([0.1f, 0.2f]);
        }
    }

    private sealed class RecordingVectorStore(params VectorSearchResult[] results) : IVectorStore
    {
        public VectorSearchRequest? Request { get; private set; }

        public Task UpsertAsync(
            IReadOnlyList<EmbeddedDocumentChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteByDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
            VectorSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;

            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
        }
    }

    private sealed class RecordingAnswerService(string answer) : IAnswerGenerationService
    {
        public AnswerGenerationRequest? Request { get; private set; }

        public Task<string> GenerateAnswerAsync(
            AnswerGenerationRequest request,
            Func<string, Task>? onDelta = null,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            if (onDelta is not null)
            {
                return InvokeAndReturnAsync(onDelta, answer);
            }

            return Task.FromResult(answer);
        }

        private static async Task<string> InvokeAndReturnAsync(
            Func<string, Task> onDelta,
            string answerText)
        {
            await onDelta(answerText);
            return answerText;
        }
    }
}
