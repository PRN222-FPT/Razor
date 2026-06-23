namespace ServiceLayer.DTOs;

public sealed record AdminUserSummaryDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    string? InstitutionalId,
    bool IsBlocked,
    DateTime? CreatedAt);

public sealed record AdminUserManagementDto(
    int TotalUsers,
    int TotalStudents,
    int TotalTeachers,
    int ActiveUsers,
    IReadOnlyList<AdminUserSummaryDto> Users);

public sealed record UpdateAccountStatusResult(
    bool Succeeded,
    string? ErrorMessage = null)
{
    public static UpdateAccountStatusResult Success() => new(true);

    public static UpdateAccountStatusResult Failure(string message) => new(false, message);
}

public sealed record AdminSubjectSummaryDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    string? Description,
    int AssignedTeacherCount,
    bool HasLeader,
    string? LeaderName);

public sealed record AdminTeacherSummaryDto(
    Guid TeacherId,
    string FullName,
    string Email,
    string Department,
    IReadOnlyList<string> SubjectAssignments);

public sealed record CreateSubjectRequest(
    string? SubjectCode,
    string? SubjectName,
    string? Description,
    IReadOnlyList<Guid> AssignedTeacherIds);

public sealed record CreateSubjectResult(
    bool Succeeded,
    Guid? SubjectId = null,
    string? ErrorMessage = null)
{
    public static CreateSubjectResult Success(Guid subjectId) => new(true, subjectId);

    public static CreateSubjectResult Failure(string message) => new(false, null, message);
}

public sealed record DeleteSubjectResult(
    bool Succeeded,
    string? ErrorMessage = null)
{
    public static DeleteSubjectResult Success() => new(true);

    public static DeleteSubjectResult Failure(string message) => new(false, message);
}

public sealed record CreateTeacherRequest(
    string Email,
    Guid? SubjectId,
    bool IsSubjectLeader);

public sealed record CreateTeacherResult(
    bool Succeeded,
    Guid? TeacherUserId = null,
    string? ErrorMessage = null)
{
    public static CreateTeacherResult Success(Guid teacherUserId) => new(true, teacherUserId);

    public static CreateTeacherResult Failure(string message) => new(false, null, message);
}

public sealed record ImportStudentsRequest(
    string OriginalFileName,
    Stream Content);

public sealed record ImportStudentsResult(
    bool Succeeded,
    int CreatedCount,
    int SkippedCount,
    IReadOnlyList<string> Errors,
    string? ErrorMessage = null)
{
    public static ImportStudentsResult Success(
        int createdCount,
        int skippedCount,
        IReadOnlyList<string> errors) =>
        new(true, createdCount, skippedCount, errors);

    public static ImportStudentsResult Failure(string message) =>
        new(false, 0, 0, [], message);
}
