using Microsoft.AspNetCore.SignalR;
using Razor.Hubs;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Services;

public sealed class SignalRTeacherSubjectRealtimeNotifier(
    IHubContext<DocumentProcessingHub> hubContext) : ITeacherSubjectRealtimeNotifier
{
    public async Task NotifySubjectAssignedAsync(
        TeacherSubjectAssignedNotification notification,
        CancellationToken cancellationToken = default)
    {
        foreach (var teacherId in notification.TeacherIds.Distinct())
        {
            await hubContext.Clients
                .Group(DocumentProcessingHubGroups.ForTeacher(teacherId))
                .SendAsync("subjectAssigned", notification, cancellationToken);
        }
    }

    public async Task NotifySubjectDeletedAsync(
        TeacherSubjectDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        foreach (var teacherId in notification.TeacherIds.Distinct())
        {
            await hubContext.Clients
                .Group(DocumentProcessingHubGroups.ForTeacher(teacherId))
                .SendAsync("subjectDeleted", notification, cancellationToken);
        }
    }
}
