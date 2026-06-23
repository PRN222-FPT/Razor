namespace ServiceLayer.DTOs;

public sealed class RagChatOptions
{
    public const string SectionName = "RagChat";

    public int SearchLimit { get; set; } = 4;

    public double MinimumScore { get; set; } = 0.2;

    public int MaxQuestionLength { get; set; } = 1000;

    public int MaxContextCharacters { get; set; } = 6000;
}

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "openai/gpt-4o-mini";

    public double Temperature { get; set; } = 0.2;

    public int MaxOutputTokens { get; set; } = 2400;

    public string AppName { get; set; } = "FPT UniRAG";

    public string SiteUrl { get; set; } = string.Empty;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed record StudentChatSubjectDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName);

public sealed record StudentChatDocumentDto(
    Guid DocumentId,
    Guid SubjectId,
    string Title,
    string? ChapterTitle,
    DateTime? CreatedAt,
    string Status,
    string? FileType);

public sealed record StudentChatDocumentSubjectGroupDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    IReadOnlyList<StudentChatDocumentDto> Documents);

public sealed record StudentChatDocumentLibraryDto(
    IReadOnlyList<StudentChatDocumentSubjectGroupDto> Subjects,
    int TotalDocuments);

public sealed record StudentChatMessageDto(
    Guid MessageId,
    string SenderRole,
    string MessageContent,
    DateTime? CreatedAt,
    IReadOnlyList<StudentChatCitationDto> Citations);

public sealed record StudentChatSessionDto(
    Guid SessionId,
    Guid? SubjectId,
    string Title,
    string Preview,
    DateTime? StartedAt,
    int MessageCount);

public sealed record StudentChatPageDto(
    Guid? SessionId,
    IReadOnlyList<StudentChatSubjectDto> Subjects,
    IReadOnlyList<StudentChatMessageDto> Messages,
    StudentChatDocumentLibraryDto DocumentLibrary,
    Guid? SubjectId = null,
    IReadOnlyList<StudentChatSessionDto>? Sessions = null);

public sealed record StudentChatRequest(
    Guid UserId,
    Guid? SessionId,
    Guid SubjectId,
    string Question);

public sealed record StudentChatAnswerDto(
    Guid SessionId,
    string Answer,
    IReadOnlyList<StudentChatCitationDto> Citations,
    IReadOnlyList<StudentChatMessageDto> Messages);

public sealed record StudentChatCitationDto(
    Guid ChunkId,
    string DocumentTitle,
    string SubjectCode,
    string ChapterTitle,
    int ChunkIndex,
    string Excerpt,
    double Score);

public sealed record VectorSearchRequest(
    IReadOnlyList<float> Vector,
    Guid SubjectId,
    int Limit);

public sealed record VectorSearchResult(
    Guid ChunkId,
    Guid DocumentId,
    Guid SubjectId,
    Guid ChapterId,
    string DocumentTitle,
    int ChunkIndex,
    double Score);

public sealed record RetrievedChatContext(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    string SubjectCode,
    string SubjectName,
    string ChapterTitle,
    int ChunkIndex,
    string Content,
    double Score);

public sealed record AnswerGenerationRequest(
    string Question,
    IReadOnlyList<RetrievedChatContext> Contexts);
