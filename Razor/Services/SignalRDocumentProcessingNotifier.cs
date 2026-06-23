using Microsoft.AspNetCore.SignalR;
using Razor.Hubs;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Services;

public sealed class SignalRDocumentProcessingNotifier(
    IHubContext<DocumentProcessingHub> hubContext) : IDocumentProcessingNotifier
{
    public Task NotifyAsync(
        DocumentProcessingStatusNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (!notification.TeacherId.HasValue)
        {
            return Task.CompletedTask;
        }

        return hubContext.Clients
            .Group(DocumentProcessingHubGroups.ForTeacher(notification.TeacherId.Value))
            .SendAsync("documentProcessingUpdated", notification, cancellationToken);
    }
}
