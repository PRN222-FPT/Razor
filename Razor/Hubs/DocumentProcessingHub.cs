using System.Security.Claims;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Razor.Hubs;

[Authorize(Policy = "TeacherOnly")]
public sealed class DocumentProcessingHub(IUnitOfWork unitOfWork) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var teacherId = await GetCurrentTeacherIdAsync(Context.User, Context.ConnectionAborted);
        if (teacherId.HasValue)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                DocumentProcessingHubGroups.ForTeacher(teacherId.Value),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    private async Task<Guid?> GetCurrentTeacherIdAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var email = user?.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await unitOfWork.Repository<Teacher>()
            .Query()
            .AsNoTracking()
            .Where(teacher => teacher.Email != null
                && teacher.Email.ToLower() == normalizedEmail)
            .Select(teacher => (Guid?)teacher.TeacherId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

public static class DocumentProcessingHubGroups
{
    public static string ForTeacher(Guid teacherId)
    {
        return $"teacher-documents:{teacherId:N}";
    }
}
