using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages
{
    public class IndexModel(IAuthService authService) : PageModel
    {
        [BindProperty]
        public LoginInputModel Input { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        public IActionResult OnGet(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirstValue(ClaimTypes.Role);
                if (!string.IsNullOrWhiteSpace(role))
                {
                    return RedirectToRoleHome(role);
                }
            }

            Input.ReturnUrl = returnUrl;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await authService.ValidateCredentialsAsync(
                new LoginRequest(Input.Email, Input.Password),
                cancellationToken);

            if (!result.Succeeded || result.User is null)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid email or password.");
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.UserId.ToString()),
                new(ClaimTypes.Name, result.User.FullName),
                new(ClaimTypes.Email, result.User.Email),
                new(ClaimTypes.Role, result.User.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = Input.RememberMe
                });

            if (!string.IsNullOrWhiteSpace(Input.ReturnUrl) && Url.IsLocalUrl(Input.ReturnUrl))
            {
                return LocalRedirect(Input.ReturnUrl);
            }

            return RedirectToRoleHome(result.User.Role);
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToPage("/Index");
        }

        private IActionResult RedirectToRoleHome(string role)
        {
            return role switch
            {
                "admin" => RedirectToPage("/Admin/Portal"),
                "teacher" => RedirectToPage("/Teacher/Dashboard"),
                _ => RedirectToPage("/Student/Chat")
            };
        }

        public sealed class LoginInputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }

            public string? ReturnUrl { get; set; }
        }
    }
}
