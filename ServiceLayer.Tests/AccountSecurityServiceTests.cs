using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class AccountSecurityServiceTests
{
    [Fact]
    public async Task ChangePasswordAsync_ChangesPasswordAndInvalidatesOldPassword()
    {
        await using var context = CreateContext();
        var user = CreateUser("student@example.com", "student", "OldPassword123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var authService = CreateAuthService(context);

        var result = await service.ChangePasswordAsync(
            new ChangePasswordRequest(user.UserId, "OldPassword123!", "NewPassword123!"));

        Assert.True(result.Succeeded);

        var oldLogin = await authService.ValidateCredentialsAsync(
            new LoginRequest(user.Email, "OldPassword123!"));
        Assert.False(oldLogin.Succeeded);

        var newLogin = await authService.ValidateCredentialsAsync(
            new LoginRequest(user.Email, "NewPassword123!"));
        Assert.True(newLogin.Succeeded);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_ReturnsTokenForStudentAccount()
    {
        await using var context = CreateContext();
        var user = CreateUser("student.reset@example.com", "student", "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.RequestPasswordResetAsync(user.Email);

        Assert.True(result.Succeeded);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.FullName, result.FullName);
        Assert.False(string.IsNullOrWhiteSpace(result.ResetToken));

        var stored = await context.Users.SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(stored.PasswordResetTokenHash));
        Assert.NotNull(stored.PasswordResetTokenExpiresAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_UsesResetTokenAndClearsIt()
    {
        await using var context = CreateContext();
        var user = CreateUser("teacher.reset@example.com", "teacher", "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var requestResult = await service.RequestPasswordResetAsync(user.Email);
        Assert.True(requestResult.Succeeded);
        Assert.NotNull(requestResult.ResetToken);

        var resetResult = await service.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email, requestResult.ResetToken!, "NewPassword123!"));

        Assert.True(resetResult.Succeeded);

        var stored = await context.Users.SingleAsync();
        Assert.Null(stored.PasswordResetTokenHash);
        Assert.Null(stored.PasswordResetTokenExpiresAt);

        var authService = CreateAuthService(context);
        var loginResult = await authService.ValidateCredentialsAsync(
            new LoginRequest(user.Email, "NewPassword123!"));

        Assert.True(loginResult.Succeeded);
    }

    [Fact]
    public async Task ResetPasswordAsync_FailsWhenTokenIsExpired()
    {
        await using var context = CreateContext();
        var user = CreateUser("student.expired@example.com", "student", "Password123!");
        user.PasswordResetTokenHash = ComputeTokenHash("expired-token");
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email, "expired-token", "NewPassword123!"));

        Assert.False(result.Succeeded);
        Assert.Equal("The reset link is invalid or expired.", result.ErrorMessage);
    }

    private static AccountSecurityService CreateService(AppDbContext context)
    {
        return new AccountSecurityService(
            new UnitOfWork(context),
            new PasswordHasher<User>());
    }

    private static AuthService CreateAuthService(AppDbContext context)
    {
        return new AuthService(
            new UnitOfWork(context),
            new PasswordHasher<User>(),
            NullLogger<AuthService>.Instance);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static User CreateUser(string email, string role, string password)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = email.Split('@')[0].Replace('.', ' '),
            Email = email,
            Role = role,
            IsBlocked = false
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);

        return user;
    }

    private static string ComputeTokenHash(string token)
    {
        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token)));
    }
}
