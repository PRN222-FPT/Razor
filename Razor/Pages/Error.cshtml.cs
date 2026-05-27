using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace Razor.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public int? ErrorStatusCode { get; private set; }

        public string ErrorMessage { get; private set; } = "An unexpected error occurred. Please try again later.";

        public void OnGet(int? statusCode = null)
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            ErrorStatusCode = statusCode;
            ErrorMessage = statusCode switch
            {
                400 => "The request was invalid.",
                403 => "You do not have permission to access this resource.",
                404 => "The requested resource was not found.",
                409 => "The request could not be completed due to a conflict.",
                499 => "The request was cancelled.",
                501 => "This feature is not implemented yet.",
                _ => ErrorMessage
            };
        }
    }

}
