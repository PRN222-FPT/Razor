using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IAdminUserService
{
    Task<AdminUserManagementDto> GetUserManagementAsync(
        string? searchTerm,
        string? roleFilter,
        int take = 12,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminSubjectSummaryDto>> GetSubjectSummariesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminTeacherSummaryDto>> GetTeacherSummariesAsync(
        CancellationToken cancellationToken = default);

    Task<CreateSubjectResult> CreateSubjectAsync(
        CreateSubjectRequest request,
        CancellationToken cancellationToken = default);

    Task<DeleteSubjectResult> DeleteSubjectAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task<CreateTeacherResult> CreateTeacherAsync(
        CreateTeacherRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateAccountStatusResult> SuspendAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UpdateAccountStatusResult> ReactivateAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UpdateAccountStatusResult> ResetAccountPasswordAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ImportStudentsResult> ImportStudentsAsync(
        ImportStudentsRequest request,
        CancellationToken cancellationToken = default);
}
