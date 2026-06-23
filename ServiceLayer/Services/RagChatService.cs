using System.Text.Json;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class RagChatService(
    IUnitOfWork unitOfWork,
    IEmbeddingService embeddingService,
    IVectorStore vectorStore,
    IAnswerGenerationService answerService,
    IOptions<RagChatOptions> options,
    ILogger<RagChatService> logger) : IRagChatService
{
    private const string StudentRole = "student";
    private const string AssistantRole = "assistant";
    private static readonly JsonSerializerOptions CitationJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StudentChatPageDto> GetChatPageAsync(
        Guid userId,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var subjects = await LoadSubjectsAsync(cancellationToken);
        var documentLibrary = await LoadDocumentLibraryAsync(subjects, cancellationToken);
        var sessions = await LoadSessionsAsync(userId, cancellationToken);
        var requestedSession = await LoadSessionAsync(userId, sessionId, cancellationToken);
        var session = requestedSession is null
            ? sessions.FirstOrDefault()
            : sessions.FirstOrDefault(item => item.SessionId == requestedSession.SessionId)
            ?? sessions.FirstOrDefault();

        var messages = session is null
            ? []
            : await LoadMessagesAsync(session.SessionId, cancellationToken);

        return new StudentChatPageDto(
            session?.SessionId,
            subjects,
            messages,
            documentLibrary,
            session?.SubjectId,
            sessions);
    }

    public async Task<Guid> StartNewSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await CreateSessionAsync(userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return session.SessionId;
    }

    public async Task<StudentChatAnswerDto> AskAsync(
        StudentChatRequest request,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default)
    {
        var question = NormalizeQuestion(request.Question);
        if (question.Length == 0)
        {
            throw new ArgumentException("Question must not be empty.", nameof(request));
        }

        var maxQuestionLength = Math.Max(100, options.Value.MaxQuestionLength);
        if (question.Length > maxQuestionLength)
        {
            throw new ArgumentException($"Question must be {maxQuestionLength} characters or fewer.", nameof(request));
        }

        var subjectExists = await unitOfWork.Repository<Subject>()
            .Query()
            .AsNoTracking()
            .AnyAsync(subject => subject.SubjectId == request.SubjectId, cancellationToken);
        if (!subjectExists)
        {
            throw new InvalidOperationException("Selected subject was not found.");
        }

        var session = await GetOrCreateSessionAsync(request.UserId, request.SessionId, cancellationToken);
        EnsureSessionSubject(session, request.SubjectId);
        await AddMessageAsync(session.SessionId, StudentRole, question, [], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var queryVector = await embeddingService.EmbedQueryAsync(question, cancellationToken);
        var vectorResults = await vectorStore.SearchAsync(
            new VectorSearchRequest(
                queryVector,
                request.SubjectId,
                Math.Max(1, options.Value.SearchLimit)),
            cancellationToken);
        var contexts = await LoadContextsAsync(vectorResults, cancellationToken);
        contexts = contexts
            .Where(context => context.Score >= options.Value.MinimumScore)
            .ToList();

        var answer = await answerService.GenerateAnswerAsync(
            new AnswerGenerationRequest(question, contexts),
            onDelta,
            cancellationToken);

        var citations = contexts.Select(ToCitation).ToList();

        await AddMessageAsync(session.SessionId, AssistantRole, answer, citations, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "RAG answer generated. SessionId={SessionId}, SubjectId={SubjectId}, ContextCount={ContextCount}",
            session.SessionId,
            request.SubjectId,
            contexts.Count);

        var messages = await LoadMessagesAsync(session.SessionId, cancellationToken);

        return new StudentChatAnswerDto(
            session.SessionId,
            answer,
            citations,
            messages);
    }

    private async Task<IReadOnlyList<StudentChatSessionDto>> LoadSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sessions = await unitOfWork.Repository<Session>()
            .Query()
            .Include(session => session.Messages)
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.SessionId)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(session =>
            {
                var titleSource = session.Messages
                    .Where(message => message.SenderRole == StudentRole)
                    .OrderBy(message => message.CreatedAt)
                    .Select(message => message.MessageContent)
                    .FirstOrDefault()
                    ?? session.Messages
                        .OrderBy(message => message.CreatedAt)
                        .Select(message => message.MessageContent)
                        .FirstOrDefault()
                    ?? "New chat";

                var preview = session.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.MessageContent)
                    .FirstOrDefault() ?? "No messages yet";

                return new StudentChatSessionDto(
                    session.SessionId,
                    session.SubjectId,
                    CreateSessionTitle(titleSource),
                    preview,
                    session.StartedAt,
                    session.Messages.Count);
            })
            .ToList();
    }

    private async Task<Session?> LoadSessionAsync(
        Guid userId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        if (!sessionId.HasValue)
        {
            return null;
        }

        return await unitOfWork.Repository<Session>()
            .Query()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                session => session.SessionId == sessionId.Value && session.UserId == userId,
                cancellationToken);
    }

    private async Task<IReadOnlyList<StudentChatSubjectDto>> LoadSubjectsAsync(
        CancellationToken cancellationToken)
    {
        return await unitOfWork.Repository<Subject>()
            .Query()
            .AsNoTracking()
            .OrderBy(subject => subject.SubjectCode)
            .Select(subject => new StudentChatSubjectDto(
                subject.SubjectId,
                subject.SubjectCode,
                subject.SubjectName))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<StudentChatMessageDto>> LoadMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var messages = await unitOfWork.Repository<Message>()
            .Query()
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

        return messages
            .Select(message => new StudentChatMessageDto(
                message.MessageId,
                message.SenderRole,
                message.MessageContent,
                message.CreatedAt,
                DeserializeCitations(message.CitationsJson)))
            .ToList();
    }

    private async Task<StudentChatDocumentLibraryDto> LoadDocumentLibraryAsync(
        IReadOnlyList<StudentChatSubjectDto> subjects,
        CancellationToken cancellationToken)
    {
        var documents = await unitOfWork.Repository<Document>()
            .Query()
            .AsNoTracking()
            .Include(document => document.Chapter)
            .OrderBy(document => document.Subject.SubjectCode)
            .ThenBy(document => document.Subject.SubjectName)
            .ThenByDescending(document => document.CreatedAt)
            .ThenBy(document => document.Title)
            .Select(document => new StudentChatDocumentDto(
                document.DocumentId,
                document.SubjectId,
                document.Title,
                document.Chapter.ChapterTitle,
                document.CreatedAt,
                string.IsNullOrWhiteSpace(document.Status) ? "pending" : document.Status,
                document.FileType))
            .ToListAsync(cancellationToken);

        var groupedDocuments = documents
            .GroupBy(document => document.SubjectId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<StudentChatDocumentDto>)group.ToList());

        var subjectGroups = subjects
            .Select(subject => new StudentChatDocumentSubjectGroupDto(
                subject.SubjectId,
                subject.SubjectCode,
                subject.SubjectName,
                groupedDocuments.GetValueOrDefault(subject.SubjectId, [])))
            .ToList();

        return new StudentChatDocumentLibraryDto(subjectGroups, documents.Count);
    }

    private async Task<Session> GetOrCreateSessionAsync(
        Guid userId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId.HasValue)
        {
            var existing = await unitOfWork.Repository<Session>()
                .Query()
                .SingleOrDefaultAsync(
                    session => session.SessionId == sessionId.Value && session.UserId == userId,
                    cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        return await CreateSessionAsync(userId, cancellationToken);
    }

    private static void EnsureSessionSubject(Session session, Guid subjectId)
    {
        if (session.SubjectId is null)
        {
            session.SubjectId = subjectId;
            return;
        }

        if (session.SubjectId.Value != subjectId)
        {
            throw new InvalidOperationException(
                "This chat session is already tied to a different subject. Start a new chat to switch subjects.");
        }
    }

    private async Task<Session> CreateSessionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            UserId = userId,
            StartedAt = CurrentTimestamp()
        };
        await unitOfWork.Repository<Session>().AddAsync(session, cancellationToken);

        return session;
    }

    private async Task AddMessageAsync(
        Guid sessionId,
        string senderRole,
        string content,
        IReadOnlyList<StudentChatCitationDto> citations,
        CancellationToken cancellationToken)
    {
        await unitOfWork.Repository<Message>().AddAsync(
            new Message
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                SenderRole = senderRole,
                MessageContent = content,
                CitationsJson = citations.Count == 0
                    ? (senderRole.Equals(AssistantRole, StringComparison.OrdinalIgnoreCase) ? "[]" : null)
                    : SerializeCitations(citations),
                CreatedAt = CurrentTimestamp()
            },
            cancellationToken);
    }

    private async Task<IReadOnlyList<RetrievedChatContext>> LoadContextsAsync(
        IReadOnlyList<VectorSearchResult> results,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return [];
        }

        var chunkIds = results.Select(result => result.ChunkId).Distinct().ToList();
        var chunks = await unitOfWork.Repository<Chunk>()
            .Query()
            .AsNoTracking()
            .Include(chunk => chunk.Document)
            .ThenInclude(document => document.Subject)
            .Include(chunk => chunk.Document)
            .ThenInclude(document => document.Chapter)
            .Where(chunk => chunkIds.Contains(chunk.ChunkId))
            .ToListAsync(cancellationToken);
        var chunksById = chunks.ToDictionary(chunk => chunk.ChunkId);

        return results
            .Where(result => chunksById.ContainsKey(result.ChunkId))
            .Select(result =>
            {
                var chunk = chunksById[result.ChunkId];
                return new RetrievedChatContext(
                    chunk.ChunkId,
                    chunk.DocumentId,
                    chunk.Document.Title,
                    chunk.Document.Subject.SubjectCode,
                    chunk.Document.Subject.SubjectName,
                    chunk.Document.Chapter.ChapterTitle,
                    chunk.ChunkIndex,
                    chunk.Content,
                    result.Score);
            })
            .ToList();
    }

    private static StudentChatCitationDto ToCitation(RetrievedChatContext context)
    {
        return new StudentChatCitationDto(
            context.ChunkId,
            context.DocumentTitle,
            context.SubjectCode,
            context.ChapterTitle,
            context.ChunkIndex,
            CreateExcerpt(context.Content),
            context.Score);
    }

    private static string SerializeCitations(IReadOnlyList<StudentChatCitationDto> citations)
    {
        return JsonSerializer.Serialize(citations, CitationJsonOptions);
    }

    private static IReadOnlyList<StudentChatCitationDto> DeserializeCitations(string? citationsJson)
    {
        if (string.IsNullOrWhiteSpace(citationsJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<StudentChatCitationDto>>(citationsJson, CitationJsonOptions) ?? [];
    }

    private static string NormalizeQuestion(string question)
    {
        return question.ReplaceLineEndings(" ").Trim();
    }

    private static string CreateExcerpt(string content)
    {
        var normalized = content.ReplaceLineEndings(" ").Trim();

        return normalized.Length <= 220
            ? normalized
            : $"{normalized[..220]}...";
    }

    private static string CreateSessionTitle(string title)
    {
        var normalized = NormalizeQuestion(title);
        if (normalized.Length <= 48)
        {
            return normalized;
        }

        return $"{normalized[..45].TrimEnd()}...";
    }

    private static DateTime CurrentTimestamp()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }
}
