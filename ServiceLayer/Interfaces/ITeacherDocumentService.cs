using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface ITeacherDocumentService
{
    Task<TeacherDocumentDashboardDto> GetDashboardAsync(
        string teacherEmail,
        CancellationToken cancellationToken = default);

    Task<TeacherDocumentListDto> GetDocumentListAsync(
        string teacherEmail,
        CancellationToken cancellationToken = default);

    Task<TeacherDocumentDetailsDto?> GetDocumentDetailsAsync(
        string teacherEmail,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<UploadTeacherDocumentResult> UploadAsync(
        UploadTeacherDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<DeleteTeacherDocumentResult> DeleteAsync(
        Guid currentUserId,
        string teacherEmail,
        Guid documentId,
        CancellationToken cancellationToken = default);
}
