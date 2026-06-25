namespace ServiceLayer.DTOs;

public sealed class TeacherDocumentUploadOptions
{
    public const string SectionName = "TeacherDocumentUpload";

    public long MaxFileSizeBytes { get; set; } = 52_428_800;

    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx"];

    public string StorageRootPath { get; set; } = string.Empty;
}

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public int ChunkSize { get; set; } = 1400;

    public int ChunkOverlap { get; set; } = 180;

    public string[] Separators { get; set; } = ["\r\n", "\n\n", "\n", " ", string.Empty];

    public int QueuePollIntervalSeconds { get; set; } = 30;

    public int MaxEmbeddingBatchSize { get; set; } = 8;

    public bool EnablePdfOcr { get; set; } = true;

    public int MinimumEmbeddedTextCharacters { get; set; } = 100;

    public string TessDataPath { get; set; } = string.Empty;

    public string OcrLanguages { get; set; } = "eng+vie";

    public int PdfOcrRenderDpi { get; set; } = 300;

    public int MaxOcrPages { get; set; } = 50;
}

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = "gemini-embedding-2";

    public int OutputDimensionality { get; set; } = 768;
}

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Endpoint { get; set; } = "http://localhost:6333";

    public string ApiKey { get; set; } = string.Empty;

    public string CollectionName { get; set; } = "document_chunks";
}

public sealed record TeacherDocumentDashboardDto(
    IReadOnlyList<TeacherUploadSubjectDto> Subjects,
    IReadOnlyList<TeacherDocumentRowDto> RecentDocuments,
    int TotalDocuments,
    int QueuedDocuments,
    long UsedStorageBytes,
    long MaxStorageBytes);

public sealed record TeacherDocumentListDto(
    IReadOnlyList<TeacherDocumentRowDto> Documents);

public sealed record TeacherUploadSubjectDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName);

public sealed record TeacherDocumentRowDto(
    Guid DocumentId,
    string Title,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    string ChapterTitle,
    string FileName,
    Guid? UploadedById,
    string UploadedByName,
    DateTime? CreatedAt,
    string Status,
    string? FileType);

public sealed record TeacherDocumentDetailsDto(
    Guid DocumentId,
    string Title,
    string SubjectCode,
    string SubjectName,
    string ChapterTitle,
    DateTime? CreatedAt,
    string Status,
    string? FileType,
    string? ErrorMessage,
    IReadOnlyList<TeacherDocumentChunkDto> Chunks);

public sealed record TeacherDocumentChunkDto(
    Guid ChunkId,
    int ChunkIndex,
    string Content,
    DateTime? CreatedAt);

public sealed record UploadTeacherDocumentRequest(
    Guid UserId,
    string UserEmail,
    string? Title,
    Guid SubjectId,
    string? ChapterTitle,
    string OriginalFileName,
    long FileLength,
    Stream Content);

public sealed record UploadTeacherDocumentResult(
    bool Succeeded,
    Guid? DocumentId = null,
    string? ErrorMessage = null)
{
    public static UploadTeacherDocumentResult Success(Guid documentId) => new(true, documentId);

    public static UploadTeacherDocumentResult Failure(string message) => new(false, null, message);
}

public sealed record DeleteTeacherDocumentResult(
    bool Succeeded,
    string? ErrorMessage = null)
{
    public static DeleteTeacherDocumentResult Success() => new(true);

    public static DeleteTeacherDocumentResult Failure(string message) => new(false, message);
}

public sealed record ParsedDocumentPage(int PageNumber, string Text);

public sealed record ParsedDocumentContent(IReadOnlyList<ParsedDocumentPage> Pages)
{
    public ParsedDocumentContent(string text)
        : this([new ParsedDocumentPage(1, text)])
    {
    }

    public string Text => string.Join(
        Environment.NewLine + Environment.NewLine,
        Pages.OrderBy(page => page.PageNumber).Select(page => page.Text));
}

public sealed record DocumentChunkDraft(int ChunkIndex, string Content);

public sealed record EmbeddedDocumentChunk(
    Guid ChunkId,
    Guid DocumentId,
    Guid SubjectId,
    Guid ChapterId,
    string DocumentTitle,
    int ChunkIndex,
    string Content,
    IReadOnlyList<float> Vector);

public sealed record DocumentProcessingStatusNotification(
    Guid DocumentId,
    Guid? TeacherId,
    string Status,
    string StatusLabel,
    string StatusClass,
    int? ChunkCount = null,
    string? ErrorMessage = null);
