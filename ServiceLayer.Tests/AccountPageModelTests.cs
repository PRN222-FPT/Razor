using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Razor.Pages.Account;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class AccountPageModelTests
{
    [Fact]
    public async Task ChangePassword_OnPostAsync_ReturnsForbidWhenUserIdClaimMissing()
    {
        var model = new ChangePasswordModel(new RecordingAccountSecurityService());
        model.PageContext = CreatePageContext();
        model.Input.CurrentPassword = "OldPassword123!";
        model.Input.NewPassword = "NewPassword123!";
        model.Input.ConfirmNewPassword = "NewPassword123!";

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ChangePassword_OnPostAsync_UpdatesPasswordAndRedirects()
    {
        var service = new RecordingAccountSecurityService
        {
            ChangePasswordResult = ChangePasswordResult.Success()
        };
        var model = new ChangePasswordModel(service);
        model.PageContext = CreatePageContext(Guid.NewGuid());
        model.Input.CurrentPassword = "OldPassword123!";
        model.Input.NewPassword = "NewPassword123!";
        model.Input.ConfirmNewPassword = "NewPassword123!";

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/ChangePassword", redirect.PageName);
        Assert.Equal(1, service.ChangePasswordCalls);
    }

    [Fact]
    public void ResetPassword_OnGetAsync_RedirectsToForgotPasswordWhenMissingQuery()
    {
        var model = new ResetPasswordModel(new RecordingAccountSecurityService());
        model.PageContext = CreatePageContext();

        var result = model.OnGet();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/ForgotPassword", redirect.PageName);
    }

    [Fact]
    public async Task ResetPassword_OnPostAsync_UpdatesPasswordAndRedirectsToLogin()
    {
        var service = new RecordingAccountSecurityService
        {
            ResetPasswordResult = ResetPasswordResult.Success()
        };
        var model = new ResetPasswordModel(service);
        model.PageContext = CreatePageContext();
        model.Email = "student@example.com";
        model.Token = "token";
        model.Input.NewPassword = "NewPassword123!";
        model.Input.ConfirmNewPassword = "NewPassword123!";

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
        Assert.Equal(1, service.ResetPasswordCalls);
    }

    private static PageContext CreatePageContext(Guid? userId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                    "TestAuth"));
        }

        return new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));
    }

    private sealed class RecordingAccountSecurityService : IAccountSecurityService
    {
        public int ChangePasswordCalls { get; private set; }

        public int ResetPasswordCalls { get; private set; }

        public ChangePasswordResult ChangePasswordResult { get; set; } = ChangePasswordResult.Success();

        public PasswordResetRequestResult RequestPasswordResetResult { get; set; } = PasswordResetRequestResult.Ignored();

        public ResetPasswordResult ResetPasswordResult { get; set; } = ResetPasswordResult.Success();

        public Task<ChangePasswordResult> ChangePasswordAsync(
            ChangePasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            ChangePasswordCalls++;
            return Task.FromResult(ChangePasswordResult);
        }

        public Task<PasswordResetRequestResult> RequestPasswordResetAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RequestPasswordResetResult);
        }

        public Task<ResetPasswordResult> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            ResetPasswordCalls++;
            return Task.FromResult(ResetPasswordResult);
        }

        public Task ClearPasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
