using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Student;

public sealed class ChatModel(
    IRagChatService chatService,
    ILogger<ChatModel> logger) : PageModel
{
    private const string ChatView = "chat";
    private const string DocumentsView = "documents";

    public IReadOnlyList<StudentChatSubjectDto> Subjects { get; private set; } = [];

    public IReadOnlyList<StudentChatMessageDto> Messages { get; private set; } = [];

    public IReadOnlyList<StudentChatCitationDto> Citations { get; private set; } = [];

    public IReadOnlyList<StudentChatSessionDto> Sessions { get; private set; } = [];

    public StudentChatDocumentLibraryDto DocumentLibrary { get; private set; } =
        new([], 0);

    public Guid? SessionId { get; private set; }

    [BindProperty(SupportsGet = true, Name = "sessionId")]
    [ValidateNever]
    public Guid? SelectedSessionId { get; set; }

    [BindProperty(SupportsGet = true, Name = "view")]
    [ValidateNever]
    public string? ViewMode { get; set; }

    [BindProperty(SupportsGet = true, Name = "draft")]
    [ValidateNever]
    public bool DraftSession { get; set; }

    public bool IsDocumentsView =>
        string.Equals(GetCurrentViewMode(), DocumentsView, StringComparison.OrdinalIgnoreCase);

    [BindProperty]
    public ChatInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Forbid();
        }

        if (!SelectedSessionId.HasValue && !DraftSession)
        {
            var sessionId = await chatService.StartNewSessionAsync(userId.Value, cancellationToken);
            SelectedSessionId = sessionId;
        }

        await LoadPageAsync(userId.Value, SelectedSessionId, DraftSession, cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Forbid();
        }

        var validationErrors = GetInputValidationErrors();
        if (validationErrors.Count > 0)
        {
            foreach (var error in validationErrors)
            {
                ModelState.AddModelError(nameof(Input), error);
            }

            await LoadPageAsync(userId.Value, Input.SessionId ?? SelectedSessionId, DraftSession, cancellationToken);

            return Page();
        }

        try
        {
            var answer = await chatService.AskAsync(
                new StudentChatRequest(
                    userId.Value,
                    Input.SessionId,
                    Input.SubjectId!.Value,
                    Input.Question),
                null,
                cancellationToken);

            var page = await chatService.GetChatPageAsync(userId.Value, answer.SessionId, cancellationToken);
            SessionId = answer.SessionId;
            SelectedSessionId = answer.SessionId;
            DraftSession = false;
            Messages = answer.Messages;
            Citations = answer.Citations;
            Subjects = page.Subjects;
            DocumentLibrary = page.DocumentLibrary;
            Sessions = page.Sessions ?? [];
            Input = new ChatInputModel
            {
                SessionId = answer.SessionId,
                SubjectId = page.SubjectId ?? Input.SubjectId
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ToUserSafeError(ex));
            await LoadPageAsync(userId.Value, Input.SessionId ?? SelectedSessionId, DraftSession, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostNewChatAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Forbid();
        }

        return RedirectToPage("/Student/Chat", new { view = ChatView });
    }

    public async Task<IActionResult> OnPostAskAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return new JsonResult(new ChatErrorResponse(["Your session has expired. Please sign in again."]))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        var validationErrors = GetInputValidationErrors();
        if (validationErrors.Count > 0)
        {
            return new JsonResult(new ChatErrorResponse(validationErrors))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        try
        {
            var answer = await chatService.AskAsync(
                new StudentChatRequest(
                    userId.Value,
                    Input.SessionId,
                    Input.SubjectId!.Value,
                    Input.Question),
                null,
                cancellationToken);

            return new JsonResult(ChatAskResponse.FromAnswer(answer));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return new JsonResult(new ChatErrorResponse([ToUserSafeError(ex)]))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }

    private async Task LoadPageAsync(Guid userId, Guid? sessionId, bool draftSession, CancellationToken cancellationToken)
    {
        var page = await chatService.GetChatPageAsync(userId, sessionId, cancellationToken);
        ViewMode = NormalizeViewMode(ViewMode);
        SessionId = draftSession ? null : page.SessionId;
        SelectedSessionId = draftSession ? null : page.SessionId;
        DraftSession = draftSession;
        Subjects = page.Subjects;
        Messages = draftSession ? [] : page.Messages;
        Sessions = page.Sessions ?? [];
        DocumentLibrary = page.DocumentLibrary;
        Citations = draftSession
            ? []
            : page.Messages
                .Where(message => message.SenderRole.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                .LastOrDefault()
                ?.Citations ?? [];
        Input.SessionId = draftSession ? null : page.SessionId;
        Input.SubjectId = page.SubjectId;
        if ((!Input.SubjectId.HasValue || Input.SubjectId.Value == Guid.Empty) && Subjects.Count > 0)
        {
            Input.SubjectId = Subjects[0].SubjectId;
        }
    }

    private Guid? GetCurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(id, out var userId)
            ? userId
            : null;
    }

    private IReadOnlyList<string> GetInputValidationErrors()
    {
        var errors = new List<string>();

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(Input);
        Validator.TryValidateObject(Input, validationContext, validationResults, validateAllProperties: true);

        errors.AddRange(
            validationResults
                .Select(result => result.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!));

        var inputPrefix = $"{nameof(Input)}.";
        foreach (var entry in ModelState)
        {
            if (!string.Equals(entry.Key, nameof(Input), StringComparison.Ordinal) &&
                !entry.Key.StartsWith(inputPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            errors.AddRange(
                entry.Value.Errors
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Select(message => message!));
        }

        return errors
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private string ToUserSafeError(Exception exception)
    {
        if (exception is InvalidOperationException &&
            exception.Message.StartsWith("OpenRouter answer generation failed", StringComparison.Ordinal))
        {
            logger.LogWarning(exception, "Student chat answer generation failed.");

            return "AI answer service is temporarily unavailable. Please try again.";
        }

        return exception.Message;
    }

    private static string NormalizeViewMode(string? viewMode)
    {
        return string.Equals(viewMode, DocumentsView, StringComparison.OrdinalIgnoreCase)
            ? DocumentsView
            : ChatView;
    }

    private string GetCurrentViewMode()
    {
        return NormalizeViewMode(ViewMode);
    }

    public static string GetDocumentFileTypeCssClass(string? fileType)
    {
        return string.Equals(fileType, "pdf", StringComparison.OrdinalIgnoreCase)
            ? "pdf"
            : "doc";
    }

    public static string GetDocumentFileTypeLabel(string? fileType)
    {
        return string.IsNullOrWhiteSpace(fileType)
            ? "FILE"
            : fileType.Trim().ToUpperInvariant();
    }

    public static string FormatDocumentStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "Pending"
            : char.ToUpperInvariant(status[0]) + status[1..].ToLowerInvariant();
    }

    public sealed class ChatInputModel
    {
        public Guid? SessionId { get; set; }

        [Required(ErrorMessage = "Please choose a subject.")]
        public Guid? SubjectId { get; set; }

        [Required(ErrorMessage = "Please enter a question.")]
        [StringLength(1000, ErrorMessage = "Question must be 1000 characters or fewer.")]
        public string Question { get; set; } = string.Empty;
    }

    public sealed record ChatAskResponse(
        Guid SessionId,
        string Answer,
        IReadOnlyList<StudentChatCitationDto> Citations)
    {
        public static ChatAskResponse FromAnswer(StudentChatAnswerDto answer)
        {
            return new ChatAskResponse(
                answer.SessionId,
                answer.Answer,
                answer.Citations);
        }
    }

    public sealed record ChatErrorResponse(IReadOnlyList<string> Errors);
}
