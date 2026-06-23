using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel(IAccountSecurityService accountSecurityService) : PageModel
{
    [BindProperty]
    public ChangePasswordInputModel Input { get; set; } = new();

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

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Forbid();
        }

        var result = await accountSecurityService.ChangePasswordAsync(
            new ChangePasswordRequest(userId, Input.CurrentPassword, Input.NewPassword),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Your password could not be changed.");
            return Page();
        }

        SuccessMessage = "Password updated.";
        return RedirectToPage("/Account/ChangePassword");
    }

    public sealed class ChangePasswordInputModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

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
