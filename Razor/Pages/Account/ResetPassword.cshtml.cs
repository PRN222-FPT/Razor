using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Account;

public sealed class ResetPasswordModel(IAccountSecurityService accountSecurityService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public ResetPasswordInputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Token))
        {
            return RedirectToPage("/Account/ForgotPassword");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await accountSecurityService.ResetPasswordAsync(
            new ResetPasswordRequest(Email, Token, Input.NewPassword),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Your password could not be reset.");
            return Page();
        }

        SuccessMessage = "Password updated. Sign in with your new password.";
        return RedirectToPage("/Index");
    }

    public sealed class ResetPasswordInputModel
    {
        [Required]
        [StringLength(255, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
