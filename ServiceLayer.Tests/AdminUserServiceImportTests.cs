using System.Text;
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

public sealed class AdminUserServiceImportTests
{
    [Fact]
    public async Task ImportStudentsAsync_CreatesValidCsvStudentsAndSkipsExistingAccounts()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Existing Student",
            Email = "existing@example.com",
            PasswordHash = "hashed",
            Role = "student",
            StudentCode = "se000001"
        });
        await context.SaveChangesAsync();
        var harness = CreateService(context);
        await using var stream = CreateStream("""
            MSSV,email,name
            SE000001,duplicate-code@example.com,Duplicate Code
            SE000002,existing@example.com,Duplicate Email
            SE000003,new.student@example.com,New Student
            SE000003,another@example.com,Duplicate In File
            """);

        var result = await harness.Service.ImportStudentsAsync(
            new ImportStudentsRequest("students.csv", stream));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(3, result.SkippedCount);
        Assert.Contains(result.Errors, error => error.Contains("MSSV already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("email already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("duplicate MSSV", StringComparison.OrdinalIgnoreCase));

        var created = await context.Users.SingleAsync(user => user.Email == "new.student@example.com");
        Assert.Equal("se000003", created.StudentCode);
        Assert.Equal("student", created.Role);
        Assert.False(created.IsBlocked);
        Assert.False(string.IsNullOrWhiteSpace(created.PasswordHash));
        var fakeSender = Assert.IsType<FakeStudentCredentialEmailSender>(harness.EmailSender);
        Assert.Single(fakeSender.SentRequests);
        Assert.Equal("new.student@example.com", fakeSender.SentRequests[0].Email);
        Assert.False(string.IsNullOrWhiteSpace(fakeSender.SentRequests[0].TemporaryPassword));
    }

    [Fact]
    public async Task ImportStudentsAsync_FailsWhenRequiredHeadersAreMissing()
    {
        await using var context = CreateContext();
        var harness = CreateService(context);
        await using var stream = CreateStream("""
            student_id,email,full_name
            SE000003,new.student@example.com,New Student
            """);

        var result = await harness.Service.ImportStudentsAsync(
            new ImportStudentsRequest("students.csv", stream));

        Assert.False(result.Succeeded);
        Assert.Contains("MSSV, email, name", result.ErrorMessage);
        Assert.Empty(context.Users);
    }

    [Fact]
    public async Task ImportStudentsAsync_RollsBackCreatedStudentWhenCredentialEmailFails()
    {
        await using var context = CreateContext();
        var sender = new ThrowingStudentCredentialEmailSender();
        var harness = CreateService(context, sender);
        await using var stream = CreateStream("""
            MSSV,email,name
            SE000010,rollback.student@example.com,Rollback Student
            """);

        var result = await harness.Service.ImportStudentsAsync(
            new ImportStudentsRequest("students.csv", stream));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Errors, error => error.Contains("credentials email could not be sent", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(context.Users);
    }

    private static AdminUserServiceHarness CreateService(
        AppDbContext context,
        IStudentCredentialEmailSender? emailSender = null)
    {
        var sender = emailSender ?? new FakeStudentCredentialEmailSender();
        var service = new AdminUserService(
            new UnitOfWork(context),
            new PasswordHasher<User>(),
            sender,
            new NoOpTeacherCredentialEmailSender(),
            NullLogger<AdminUserService>.Instance);

        return new AdminUserServiceHarness(service, sender);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content.ReplaceLineEndings("\n")));
    }

    private sealed record AdminUserServiceHarness(
        AdminUserService Service,
        IStudentCredentialEmailSender EmailSender);

    private sealed class FakeStudentCredentialEmailSender : IStudentCredentialEmailSender
    {
        public List<StudentCredentialEmailRequest> SentRequests { get; } = [];

        public Task SendAsync(StudentCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStudentCredentialEmailSender : IStudentCredentialEmailSender
    {
        public Task SendAsync(StudentCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("SMTP unavailable.");
        }
    }

    private sealed class NoOpTeacherCredentialEmailSender : ITeacherCredentialEmailSender
    {
        public Task SendAsync(TeacherCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
