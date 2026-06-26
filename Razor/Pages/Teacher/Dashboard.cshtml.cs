using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Teacher;

public sealed class DashboardModel(
    ITeacherDocumentService teacherDocumentService,
    IOptions<TeacherDocumentUploadOptions> uploadOptions) : PageModel
{
    [BindProperty]
    public UploadDocumentInputModel UploadDocument { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public TeacherDocumentDashboardDto Dashboard { get; private set; } =
        new([], [], 0, 0, 0, 0);

    public string MaxUploadSizeLabel => FormatBytes(uploadOptions.Value.MaxFileSizeBytes);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDashboardAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        if (UploadDocument.File is null)
        {
            ModelState.AddModelError(
                $"{nameof(UploadDocument)}.{nameof(UploadDocument.File)}",
                "Please choose a file to upload.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (!Guid.TryParse(userIdClaim, out var userId) || string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Your sign-in session is missing required teacher details.");
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        await using var uploadStream = UploadDocument.File!.OpenReadStream();
        var result = await teacherDocumentService.UploadAsync(
            new UploadTeacherDocumentRequest(
                userId,
                email,
                UploadDocument.Title,
                UploadDocument.SubjectId,
                UploadDocument.ChapterTitle,
                UploadDocument.File.FileName,
                UploadDocument.File.Length,
                uploadStream),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The document could not be uploaded.");
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = "Document was uploaded and queued for processing.";

        return RedirectToPage("/Teacher/Dashboard");
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
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        var result = await teacherDocumentService.DeleteAsync(currentUserId, email, documentId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The document could not be deleted.");
            await LoadDashboardAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = "Document was deleted. You can upload a new document for that chapter now.";

        return RedirectToPage("/Teacher/Dashboard");
    }

    public string FormatStorageUsage()
    {
        return $"{FormatBytes(Dashboard.UsedStorageBytes)} used";
    }

    public int GetStoragePercent()
    {
        if (Dashboard.MaxStorageBytes <= 0)
        {
            return 0;
        }

        return (int)Math.Clamp(
            Math.Round(Dashboard.UsedStorageBytes * 100d / Dashboard.MaxStorageBytes),
            0,
            100);
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

    private async Task LoadDashboardAsync(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        Dashboard = await teacherDocumentService.GetDashboardAsync(email, cancellationToken);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 MB";
        }

        var megabytes = bytes / 1024d / 1024d;

        return $"{megabytes:N1} MB";
    }

    public sealed class UploadDocumentInputModel
    {
        [StringLength(255)]
        public string? Title { get; set; }

        [Required(ErrorMessage = "Please choose a subject.")]
        [Display(Name = "Subject")]
        public Guid SubjectId { get; set; }

        [StringLength(255)]
        [Display(Name = "Chapter")]
        public string? ChapterTitle { get; set; }

        [Display(Name = "Document file")]
        public IFormFile? File { get; set; }
    }
}
