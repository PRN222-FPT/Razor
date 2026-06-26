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

public sealed class AdminUserServiceTeacherTests
{
    [Fact]
    public async Task CreateTeacherAsync_CreatesTeacherAccountFromEmailOnlyAndSendsCredentials()
    {
        await using var context = CreateContext();
        var sender = new FakeTeacherCredentialEmailSender();
        var service = CreateService(context, sender);

        var result = await service.CreateTeacherAsync(
            new CreateTeacherRequest(
                "teacher.one@example.com",
                null,
                false));

        Assert.True(result.Succeeded);
        var createdUser = await context.Users.SingleAsync();
        Assert.Equal("teacher", createdUser.Role);
        Assert.Equal("teacher.one@example.com", createdUser.Email);
        Assert.Equal("Teacher One", createdUser.FullName);
        Assert.False(string.IsNullOrWhiteSpace(createdUser.PasswordHash));

        var createdTeacher = await context.Teachers.SingleAsync();
        Assert.Equal(createdUser.FullName, createdTeacher.FullName);
        Assert.Equal(createdUser.Email, createdTeacher.Email);
        Assert.Null(createdTeacher.Department);
        Assert.Empty(await context.TeacherSubjects.ToListAsync());

        Assert.Single(sender.SentRequests);
        Assert.Equal("teacher.one@example.com", sender.SentRequests[0].Email);
        Assert.Equal(createdUser.FullName, sender.SentRequests[0].FullName);
        Assert.False(string.IsNullOrWhiteSpace(sender.SentRequests[0].TemporaryPassword));
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(
                createdUser,
                createdUser.PasswordHash,
                sender.SentRequests[0].TemporaryPassword));
    }

    [Fact]
    public async Task CreateTeacherAsync_AssignsLeaderWhenSelectedSubjectHasNoLeader()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor Pages"
        });
        await context.SaveChangesAsync();

        var sender = new FakeTeacherCredentialEmailSender();
        var service = CreateService(context, sender);

        var result = await service.CreateTeacherAsync(
            new CreateTeacherRequest(
                "leader.teacher@example.com",
                subjectId,
                true));

        Assert.True(result.Succeeded);
        var teacherSubject = await context.TeacherSubjects.SingleAsync();
        Assert.Equal(subjectId, teacherSubject.SubjectId);
        Assert.True(teacherSubject.IsHeadOfDepartment);
        Assert.Equal("leader.teacher@example.com", (await context.Teachers.SingleAsync()).Email);
        Assert.Single(sender.SentRequests);
    }

    [Fact]
    public async Task CreateTeacherAsync_FailsWhenSelectedSubjectAlreadyHasLeader()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var existingTeacherId = Guid.NewGuid();

        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor Pages"
        });
        context.Teachers.Add(new Teacher
        {
            TeacherId = existingTeacherId,
            FullName = "Existing Leader",
            Email = "leader@example.com",
            Department = null
        });
        context.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherSubjectId = Guid.NewGuid(),
            TeacherId = existingTeacherId,
            SubjectId = subjectId,
            IsHeadOfDepartment = true
        });
        await context.SaveChangesAsync();

        var sender = new FakeTeacherCredentialEmailSender();
        var service = CreateService(context, sender);

        var result = await service.CreateTeacherAsync(
            new CreateTeacherRequest(
                "new.teacher@example.com",
                subjectId,
                true));

        Assert.False(result.Succeeded);
        Assert.Equal("The selected subject already has a leader.", result.ErrorMessage);
        Assert.Equal(1, await context.Teachers.CountAsync());
        Assert.Equal(1, await context.TeacherSubjects.CountAsync());
        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Empty(sender.SentRequests);
    }

    private static AdminUserService CreateService(
        AppDbContext context,
        ITeacherCredentialEmailSender? sender = null)
    {
        return new AdminUserService(
            new UnitOfWork(context),
            new PasswordHasher<User>(),
            new NoOpStudentCredentialEmailSender(),
            sender ?? new FakeTeacherCredentialEmailSender(),
            new NoOpTeacherSubjectRealtimeNotifier(),
            NullLogger<AdminUserService>.Instance);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class NoOpStudentCredentialEmailSender : IStudentCredentialEmailSender
    {
        public Task SendAsync(StudentCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTeacherCredentialEmailSender : ITeacherCredentialEmailSender
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

        public Task NotifySubjectUpdatedAsync(
            TeacherSubjectUpdatedNotification notification,
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
