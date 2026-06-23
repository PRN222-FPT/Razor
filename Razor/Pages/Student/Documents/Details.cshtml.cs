using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Student.Documents;

public sealed class DetailsModel(
    IStudentDocumentService studentDocumentService) : PageModel
{
    public StudentDocumentDetailsDto? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        Document = await studentDocumentService.GetDocumentDetailsAsync(documentId, cancellationToken);
        if (Document is null)
        {
            return NotFound();
        }

        return Page();
    }

    public string FormatDocumentDate(DateTime? createdAt)
    {
        return createdAt.HasValue
            ? createdAt.Value.ToString("dd/MM/yyyy HH:mm")
            : "Not recorded";
    }

    public string FormatStatus()
    {
        return Document?.Status?.ToLowerInvariant() switch
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
        return Document?.Status?.ToLowerInvariant() switch
        {
            "completed" => "processed",
            "failed" => "error",
            _ => "indexing"
        };
    }
}
