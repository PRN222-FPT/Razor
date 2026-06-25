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

public sealed class AdminUserServiceAccountStatusTests
{
    [Fact]
    public async Task SuspendAccountAsync_SuspendsActiveStudentAccountAndBlocksLogin()
    {
        await using var context = CreateContext();
        var user = CreateUser("student.one@example.com", "student", false);
        SeedPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAdminService(context);
        var authService = CreateAuthService(context);

        var suspendResult = await service.SuspendAccountAsync(user.UserId);

        Assert.True(suspendResult.Succeeded);
        Assert.True((await context.Users.SingleAsync()).IsBlocked);

        var loginResult = await authService.ValidateCredentialsAsync(
            new LoginRequest(user.Email, "Password123!"));

        Assert.False(loginResult.Succeeded);
        Assert.Equal("Invalid email or password.", loginResult.ErrorMessage);
    }

    [Fact]
    public async Task ReactivateAccountAsync_ReactivatesBlockedTeacherAccountAndAllowsLogin()
    {
        await using var context = CreateContext();
        var user = CreateUser("teacher.one@example.com", "teacher", true);
        SeedPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var adminService = CreateAdminService(context);
        var authService = CreateAuthService(context);

        var reactivateResult = await adminService.ReactivateAccountAsync(user.UserId);

        Assert.True(reactivateResult.Succeeded);
        Assert.False((await context.Users.SingleAsync()).IsBlocked);

        var loginResult = await authService.ValidateCredentialsAsync(
            new LoginRequest(user.Email, "Password123!"));

        Assert.True(loginResult.Succeeded);
        Assert.NotNull(loginResult.User);
        Assert.Equal("teacher", loginResult.User!.Role);
    }

    [Fact]
    public async Task SuspendAccountAsync_FailsForAdminAccount()
    {
        await using var context = CreateContext();
        var user = CreateUser("admin@example.com", "admin", false);
        SeedPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAdminService(context);

        var result = await service.SuspendAccountAsync(user.UserId);

        Assert.False(result.Succeeded);
        Assert.Equal("Only student and teacher accounts can be suspended.", result.ErrorMessage);
        Assert.False((await context.Users.SingleAsync()).IsBlocked);
    }

    [Fact]
    public async Task ReactivateAccountAsync_FailsWhenAccountIsAlreadyActive()
    {
        await using var context = CreateContext();
        var user = CreateUser("student.two@example.com", "student", false);
        SeedPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAdminService(context);

        var result = await service.ReactivateAccountAsync(user.UserId);

        Assert.False(result.Succeeded);
        Assert.Equal("The account is already active.", result.ErrorMessage);
        Assert.False((await context.Users.SingleAsync()).IsBlocked);
    }

    [Fact]
    public async Task ResetAccountPasswordAsync_ResetsPasswordAndSendsTeacherEmail()
    {
        await using var context = CreateContext();
        var user = CreateUser("teacher.reset@example.com", "teacher", false);
        SeedPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var studentSender = new NoOpStudentCredentialEmailSender();
        var teacherSender = new RecordingTeacherCredentialEmailSender();
        var service = CreateAdminService(context, studentSender, teacherSender);
        var authService = CreateAuthService(context);

        var result = await service.ResetAccountPasswordAsync(user.UserId);

        Assert.True(result.Succeeded);
        Assert.Single(teacherSender.SentRequests);
        Assert.Equal(user.Email, teacherSender.SentRequests[0].Email);
        Assert.Equal(user.FullName, teacherSender.SentRequests[0].FullName);
        Assert.False(string.IsNullOrWhiteSpace(teacherSender.SentRequests[0].TemporaryPassword));
        Assert.NotEqual(
            "Password123!",
            teacherSender.SentRequests[0].TemporaryPassword);

        var oldLogin = await authService.ValidateCredentialsAsync(
            new LoginRequest(user.Email, "Password123!"));
        Assert.False(oldLogin.Succeeded);

        var newLogin = await authService.ValidateCredentialsAsync(
            new LoginRequest(user.Email, teacherSender.SentRequests[0].TemporaryPassword));
        Assert.True(newLogin.Succeeded);
    }

    [Fact]
    public async Task ResetAccountPasswordAsync_FailsForAdminAccount()
    {
        await using var context = CreateContext();
        var user = CreateUser("admin.reset@example.com", "admin", false);
        SeedPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAdminService(context);

        var result = await service.ResetAccountPasswordAsync(user.UserId);

        Assert.False(result.Succeeded);
        Assert.Equal("Only student and teacher accounts can have their passwords reset.", result.ErrorMessage);
        Assert.True(await context.Users.AnyAsync(item => item.Email == user.Email && item.PasswordHash != string.Empty));
    }

    private static AdminUserService CreateAdminService(AppDbContext context)
    {
        return CreateAdminService(
            context,
            new NoOpStudentCredentialEmailSender(),
            new NoOpTeacherCredentialEmailSender());
    }

    private static AdminUserService CreateAdminService(
        AppDbContext context,
        IStudentCredentialEmailSender studentSender,
        ITeacherCredentialEmailSender teacherSender)
    {
        return new AdminUserService(
            new UnitOfWork(context),
            new PasswordHasher<User>(),
            studentSender,
            teacherSender,
            new NoOpTeacherSubjectRealtimeNotifier(),
            NullLogger<AdminUserService>.Instance);
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

    private static User CreateUser(string email, string role, bool isBlocked)
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            FullName = email.Split('@')[0].Replace('.', ' '),
            Email = email,
            PasswordHash = string.Empty,
            Role = role,
            IsBlocked = isBlocked
        };
    }

    private static void SeedPassword(User user, string password)
    {
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
    }

    private sealed class NoOpStudentCredentialEmailSender : IStudentCredentialEmailSender
    {
        public Task SendAsync(StudentCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpTeacherCredentialEmailSender : ITeacherCredentialEmailSender
    {
        public Task SendAsync(TeacherCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTeacherCredentialEmailSender : ITeacherCredentialEmailSender
    {
        public List<TeacherCredentialEmailRequest> SentRequests { get; } = [];

        public Task SendAsync(TeacherCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpTeacherSubjectRealtimeNotifier : ITeacherSubjectRealtimeNotifier
    {
        public Task NotifySubjectAssignedAsync(
            TeacherSubjectAssignedNotification notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifySubjectDeletedAsync(
            TeacherSubjectDeletedNotification notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
