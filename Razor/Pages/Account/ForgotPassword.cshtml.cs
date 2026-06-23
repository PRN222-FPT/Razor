using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Account;

public sealed class ForgotPasswordModel(
    IAccountSecurityService accountSecurityService,
    IPasswordResetEmailSender passwordResetEmailSender) : PageModel
{
    [BindProperty]
    public ForgotPasswordInputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await accountSecurityService.RequestPasswordResetAsync(Input.Email, cancellationToken);
        if (result.ResetToken is null || string.IsNullOrWhiteSpace(result.Email) || string.IsNullOrWhiteSpace(result.FullName))
        {
            SuccessMessage = "If the email belongs to a student or teacher account, a reset link has been sent.";
            return RedirectToPage("/Account/ForgotPassword");
        }

        var resetLink = Url.Page(
            "/Account/ResetPassword",
            pageHandler: null,
            values: new { email = result.Email, token = result.ResetToken },
            protocol: Request.Scheme);

        if (string.IsNullOrWhiteSpace(resetLink))
        {
            await accountSecurityService.ClearPasswordResetTokenAsync(result.Email, cancellationToken);
            ModelState.AddModelError(string.Empty, "The reset link could not be generated.");
            return Page();
        }

        try
        {
            await passwordResetEmailSender.SendAsync(
                new PasswordResetEmailRequest(
                    result.FullName,
                    result.Email,
                    resetLink),
                cancellationToken);
        }
        catch (Exception)
        {
            await accountSecurityService.ClearPasswordResetTokenAsync(result.Email, cancellationToken);
            ModelState.AddModelError(string.Empty, "We could not send the reset email right now. Please try again.");
            return Page();
        }

        SuccessMessage = "If the email belongs to a student or teacher account, a reset link has been sent.";
        return RedirectToPage("/Account/ForgotPassword");
    }

    public sealed class ForgotPasswordInputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;
    }
}
