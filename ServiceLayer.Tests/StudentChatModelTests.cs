using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Razor.Pages.Student;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class StudentChatModelTests
{
    [Fact]
    public async Task OnPostNewChatAsync_CreatesDraftChatAndRedirectsToChatView()
    {
        var service = new RecordingRagChatService();
        var userId = Guid.NewGuid();
        var model = CreateModel(service, userId);
        model.ViewMode = "documents";

        var result = await model.OnPostNewChatAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Student/Chat", redirect.PageName);
        Assert.Equal("chat", redirect.RouteValues?["view"]);
        Assert.Equal(true, redirect.RouteValues?["draft"]);
        Assert.Null(service.NewSessionUserId);
        Assert.Null(service.CreatedSessionId);
    }

    [Fact]
    public async Task OnPostAskAsync_IgnoresMissingViewMode_WhenInputIsValid()
    {
        var service = new RecordingRagChatService();
        var model = CreateModel(service);
        model.ViewMode = null;
        model.Input.SubjectId = Guid.NewGuid();
        model.Input.Question = "What is polymorphism?";

        var result = await model.OnPostAskAsync(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<ChatModel.ChatAskResponse>(json.Value);

        Assert.Equal("Generated answer", response.Answer);
        Assert.Equal(model.Input.SubjectId!.Value, service.LastAnswerRequest!.SubjectId);
        Assert.Equal(model.Input.Question, service.LastAnswerRequest!.Question);
        Assert.Null(json.StatusCode);
    }

    [Fact]
    public async Task OnGetAsync_UsesSelectedSessionIdFromQuery()
    {
        var service = new RecordingRagChatService();
        var model = CreateModel(service);
        var sessionId = Guid.NewGuid();
        model.SelectedSessionId = sessionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(sessionId, service.LastPageSessionId);
    }

    [Fact]
    public async Task OnPostAskAsync_ReturnsValidationError_WhenQuestionIsMissing()
    {
        var service = new RecordingRagChatService();
        var model = CreateModel(service);
        model.ViewMode = null;
        model.Input.SubjectId = Guid.NewGuid();
        model.Input.Question = string.Empty;

        var result = await model.OnPostAskAsync(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<ChatModel.ChatErrorResponse>(json.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, json.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("Please enter a question.", StringComparison.Ordinal));
        Assert.Null(service.LastAnswerRequest);
    }

    private static ChatModel CreateModel(IRagChatService chatService, Guid? userId = null)
    {
        var model = new ChatModel(chatService, NullLogger<ChatModel>.Instance);
        var currentUserId = userId ?? Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString())],
                    "TestAuth"))
        };
        model.PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));

        return model;
    }

    private sealed class RecordingRagChatService : IRagChatService
    {
        public StudentChatRequest? LastAnswerRequest { get; private set; }

        public Guid? NewSessionUserId { get; private set; }

        public Guid? LastPageSessionId { get; private set; }

        public Guid? CreatedSessionId { get; private set; }

        public Task<StudentChatPageDto> GetChatPageAsync(
            Guid userId,
            Guid? sessionId = null,
            CancellationToken cancellationToken = default)
        {
            LastPageSessionId = sessionId;

            return Task.FromResult(new StudentChatPageDto(null, [], [], new([], 0)));
        }

        public Task<Guid> StartNewSessionAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            NewSessionUserId = userId;

            CreatedSessionId = Guid.NewGuid();

            return Task.FromResult(CreatedSessionId.Value);
        }

        public Task<StudentChatAnswerDto> AskAsync(
            StudentChatRequest request,
            Func<string, Task>? onDelta = null,
            CancellationToken cancellationToken = default)
        {
            LastAnswerRequest = request;

            return Task.FromResult(
                new StudentChatAnswerDto(
                    Guid.NewGuid(),
                    "Generated answer",
                    [],
                    []));
        }
    }
}
