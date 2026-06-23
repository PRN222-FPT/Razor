namespace ServiceLayer.DTOs;

public sealed record StudentDocumentChunkDto(
    Guid ChunkId,
    int ChunkIndex,
    string Content,
    DateTime? CreatedAt);

public sealed record StudentDocumentDetailsDto(
    Guid DocumentId,
    string Title,
    string SubjectCode,
    string SubjectName,
    string ChapterTitle,
    DateTime? CreatedAt,
    string Status,
    string? FileType,
    string? ErrorMessage,
    IReadOnlyList<StudentDocumentChunkDto> Chunks);
