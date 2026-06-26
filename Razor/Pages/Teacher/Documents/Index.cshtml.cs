using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Teacher.Documents;

public sealed class IndexModel(
    ITeacherDocumentService teacherDocumentService) : PageModel
{
    [TempData]
    public string? SuccessMessage { get; set; }

    public TeacherDocumentListDto DocumentList { get; private set; } = new([]);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDocumentsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (!Guid.TryParse(userIdClaim, out var currentUserId) || string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Your sign-in session is missing required teacher details.");
            await LoadDocumentsAsync(cancellationToken);
            return Page();
        }

        var result = await teacherDocumentService.DeleteAsync(currentUserId, email, documentId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The document could not be deleted.");
            await LoadDocumentsAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = "Document was deleted.";

        return RedirectToPage("/Teacher/Documents/Index");
    }

    public string FormatDocumentDate(DateTime? createdAt)
    {
        return createdAt.HasValue
            ? createdAt.Value.ToString("MMM dd, yyyy, HH:mm")
            : "Not recorded";
    }

    public string FormatSubject(TeacherDocumentRowDto document)
    {
        return $"{document.SubjectCode}: {document.SubjectName}";
    }

    public string FormatFileName(TeacherDocumentRowDto document)
    {
        return string.IsNullOrWhiteSpace(document.FileName)
            ? "Unknown file"
            : document.FileName;
    }

    public bool CanDelete(TeacherDocumentRowDto document)
    {
        return document.CanManage;
    }

    public string GetStatusClass(TeacherDocumentRowDto document)
    {
        return document.Status.ToLowerInvariant() switch
        {
            "completed" => "processed",
            "processing" or "queued" or "pending" => "indexing",
            "failed" => "error",
            _ => "indexing"
        };
    }

    public string FormatStatus(TeacherDocumentRowDto document)
    {
        return document.Status.ToLowerInvariant() switch
        {
            "completed" => "Processed",
            "processing" => "Processing",
            "queued" => "Queued",
            "pending" => "Pending",
            "failed" => "Failed",
            _ => "Pending"
        };
    }

    public string GetFileIconClass(TeacherDocumentRowDto document)
    {
        return document.FileType?.ToLowerInvariant() switch
        {
            "pdf" => "pdf",
            "docx" => "doc",
            _ => "txt"
        };
    }

    public string GetFileIconName(TeacherDocumentRowDto document)
    {
        return document.FileType?.ToLowerInvariant() switch
        {
            "pdf" => "picture_as_pdf",
            "docx" => "description",
            _ => "text_snippet"
        };
    }

    private async Task LoadDocumentsAsync(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        DocumentList = await teacherDocumentService.GetDocumentListAsync(email, cancellationToken);
    }
}
