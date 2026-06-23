using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Hubs;

[Authorize(Policy = "StudentOnly")]
public sealed class StudentChatHub(
    IRagChatService chatService,
    ILogger<StudentChatHub> logger) : Hub
{
    public async Task AskAsync(StudentChatStreamRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            await Clients.Caller.SendAsync(
                StudentChatHubEvents.ChatAnswerError,
                "Your session has expired. Please sign in again.",
                Context.ConnectionAborted);
            return;
        }

        try
        {
            var answer = await chatService.AskAsync(
                new StudentChatRequest(
                    userId.Value,
                    request.SessionId,
                    request.SubjectId,
                    request.Question),
                delta => Clients.Caller.SendAsync(
                    StudentChatHubEvents.ChatAnswerDelta,
                    delta,
                    Context.ConnectionAborted),
                Context.ConnectionAborted);

            await Clients.Caller.SendAsync(
                StudentChatHubEvents.ChatAnswerCompleted,
                new StudentChatStreamCompleted(
                    answer.SessionId,
                    answer.Answer,
                    answer.Citations),
                Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            var message = ToUserSafeError(ex);
            logger.LogWarning(ex, "Student chat streaming failed.");
            await Clients.Caller.SendAsync(
                StudentChatHubEvents.ChatAnswerError,
                message,
                Context.ConnectionAborted);
        }
    }

    private Guid? GetCurrentUserId()
    {
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId)
            ? userId
            : null;
    }

    private static string ToUserSafeError(Exception exception)
    {
        if (exception is InvalidOperationException &&
            exception.Message.StartsWith("OpenRouter answer generation failed", StringComparison.Ordinal))
        {
            return "AI answer service is temporarily unavailable. Please try again.";
        }

        return exception.Message;
    }
}

public static class StudentChatHubEvents
{
    public const string ChatAnswerDelta = "chatAnswerDelta";
    public const string ChatAnswerCompleted = "chatAnswerCompleted";
    public const string ChatAnswerError = "chatAnswerError";
}

public sealed record StudentChatStreamRequest(
    Guid? SessionId,
    Guid SubjectId,
    string Question);

public sealed record StudentChatStreamCompleted(
    Guid SessionId,
    string Answer,
    IReadOnlyList<StudentChatCitationDto> Citations);
