using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface ITeacherSubjectRealtimeNotifier
{
    Task NotifySubjectAssignedAsync(
        TeacherSubjectAssignedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifySubjectUpdatedAsync(
        TeacherSubjectUpdatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifySubjectDeletedAsync(
        TeacherSubjectDeletedNotification notification,
        CancellationToken cancellationToken = default);
}
