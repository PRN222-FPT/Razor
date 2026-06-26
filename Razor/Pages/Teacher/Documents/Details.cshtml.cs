using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Teacher.Documents;

public sealed class DetailsModel(
    ITeacherDocumentService teacherDocumentService) : PageModel
{
    public TeacherDocumentDetailsDto? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Forbid();
        }

        Document = await teacherDocumentService.GetDocumentDetailsAsync(
            email,
            documentId,
            cancellationToken);
        if (Document is null)
        {
            return NotFound();
        }

        return Page();
    }

    public string FormatDocumentDate(DateTime? createdAt)
    {
        return createdAt.HasValue
            ? createdAt.Value.ToString("MMM dd, yyyy, HH:mm")
            : "Not recorded";
    }

    public string FormatSubject()
    {
        return Document is null
            ? string.Empty
            : $"{Document.SubjectCode}: {Document.SubjectName}";
    }

    public string FormatStatus()
    {
        return Document?.Status.ToLowerInvariant() switch
        {
            "completed" => "Processed",
            "processing" => "Processing",
            "queued" => "Queued",
            "pending" => "Pending",
            "failed" => "Failed",
            _ => "Pending"
        };
    }

    public string GetStatusClass()
    {
        return Document?.Status.ToLowerInvariant() switch
        {
            "completed" => "processed",
            "failed" => "error",
            _ => "indexing"
        };
    }

    public string FormatChunkingStrategy()
    {
        return Document?.ChunkingStrategy switch
        {
            DocumentChunkingStrategies.Semantic => "Semantic",
            DocumentChunkingStrategies.FixedSized => "Fixed Sized",
            _ => "Recursive"
        };
    }
}
