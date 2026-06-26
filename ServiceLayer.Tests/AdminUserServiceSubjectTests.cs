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

public sealed class AdminUserServiceSubjectTests
{
    [Fact]
    public async Task CreateSubjectAsync_AssignsSelectedTeachersAndHeader()
    {
        await using var context = CreateContext();
        var existingSubjectId = Guid.NewGuid();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();
        context.Subjects.Add(new Subject
        {
            SubjectId = existingSubjectId,
            SubjectCode = "MTH101",
            SubjectName = "Mathematics"
        });
        context.Teachers.AddRange(
            new Teacher
            {
                TeacherId = teacherOneId,
                FullName = "Alice Nguyen",
                Email = "alice@example.com",
                Department = "Software"
            },
            new Teacher
            {
                TeacherId = teacherTwoId,
                FullName = "Bob Tran",
                Email = "bob@example.com",
                Department = "AI"
            });
        context.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherSubjectId = Guid.NewGuid(),
            TeacherId = teacherOneId,
            SubjectId = existingSubjectId,
            IsHeadOfDepartment = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.CreateSubjectAsync(
            new CreateSubjectRequest(
                "PRN222",
                "Razor Pages",
                "Web development subject",
                [teacherOneId, teacherTwoId],
                teacherTwoId));

        Assert.True(result.Succeeded);
        var subject = await context.Subjects.SingleAsync(item => item.SubjectCode == "PRN222");
        Assert.Equal("PRN222", subject.SubjectCode);
        Assert.Equal("Razor Pages", subject.SubjectName);
        Assert.Equal(3, await context.TeacherSubjects.CountAsync());
        Assert.Equal(2, await context.TeacherSubjects.CountAsync(item => item.TeacherId == teacherOneId));
        Assert.Equal(1, await context.TeacherSubjects.CountAsync(item => item.SubjectId == subject.SubjectId && item.IsHeadOfDepartment));
        Assert.Equal(teacherTwoId, await context.TeacherSubjects
            .Where(item => item.SubjectId == subject.SubjectId && item.IsHeadOfDepartment)
            .Select(item => item.TeacherId)
            .SingleAsync());
        Assert.All(await context.TeacherSubjects.ToListAsync(), item =>
        {
            Assert.True(item.SubjectId == subject.SubjectId || item.SubjectId == existingSubjectId);
            if (item.SubjectId == subject.SubjectId)
            {
                Assert.Equal(item.TeacherId == teacherTwoId, item.IsHeadOfDepartment);
            }
        });
    }

    [Fact]
    public async Task CreateSubjectAsync_AllowsAssignedTeachersWithoutHeader()
    {
        await using var context = CreateContext();
        var teacherOneId = Guid.NewGuid();

        context.Teachers.Add(new Teacher
        {
            TeacherId = teacherOneId,
            FullName = "Alice Nguyen",
            Email = "alice@example.com",
            Department = "Software"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.CreateSubjectAsync(
            new CreateSubjectRequest(
                "PRN223",
                "Razor Pages",
                null,
                [teacherOneId],
                null));

        Assert.True(result.Succeeded);
        var subject = await context.Subjects.SingleAsync(item => item.SubjectCode == "PRN223");
        var teacherSubject = await context.TeacherSubjects.SingleAsync(item => item.SubjectId == subject.SubjectId);
        Assert.Equal(teacherOneId, teacherSubject.TeacherId);
        Assert.False(teacherSubject.IsHeadOfDepartment);
    }

    [Fact]
    public async Task CreateSubjectAsync_AllowsCreationWithoutAssignments()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.CreateSubjectAsync(
            new CreateSubjectRequest(
                "PRN225",
                "Razor Pages",
                null,
                [],
                null));

        Assert.True(result.Succeeded);
        var subject = await context.Subjects.SingleAsync(item => item.SubjectCode == "PRN225");
        Assert.Equal("Razor Pages", subject.SubjectName);
        Assert.Empty(await context.TeacherSubjects.ToListAsync());
    }

    [Fact]
    public async Task CreateSubjectAsync_AutoAssignsHeaderTeacherWhenCheckboxIsMissing()
    {
        await using var context = CreateContext();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();

        context.Teachers.AddRange(
            new Teacher
            {
                TeacherId = teacherOneId,
                FullName = "Alice Nguyen",
                Email = "alice@example.com",
                Department = "Software"
            },
            new Teacher
            {
                TeacherId = teacherTwoId,
                FullName = "Bob Tran",
                Email = "bob@example.com",
                Department = "AI"
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.CreateSubjectAsync(
            new CreateSubjectRequest(
                "PRN224",
                "Razor Pages",
                null,
                [teacherOneId],
                teacherTwoId));

        Assert.True(result.Succeeded);
        var subject = await context.Subjects.SingleAsync(item => item.SubjectCode == "PRN224");
        Assert.Equal(2, await context.TeacherSubjects.CountAsync(item => item.SubjectId == subject.SubjectId));
        Assert.Equal(1, await context.TeacherSubjects.CountAsync(item => item.SubjectId == subject.SubjectId && item.IsHeadOfDepartment));
        Assert.Equal(teacherTwoId, await context.TeacherSubjects
            .Where(item => item.SubjectId == subject.SubjectId && item.IsHeadOfDepartment)
            .Select(item => item.TeacherId)
            .SingleAsync());
    }

    [Fact]
    public async Task CreateSubjectAsync_NotifiesAssignedTeachersAfterCreation()
    {
        await using var context = CreateContext();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();
        var notifier = new RecordingTeacherSubjectRealtimeNotifier();

        context.Teachers.AddRange(
            new Teacher
            {
                TeacherId = teacherOneId,
                FullName = "Alice Nguyen",
                Email = "alice@example.com",
                Department = "Software"
            },
            new Teacher
            {
                TeacherId = teacherTwoId,
                FullName = "Bob Tran",
                Email = "bob@example.com",
                Department = "AI"
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, notifier);

        var result = await service.CreateSubjectAsync(
            new CreateSubjectRequest(
                "PRN227",
                "Realtime Subject",
                null,
                [teacherOneId, teacherTwoId],
                teacherOneId));

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notifier.AssignedNotifications);
        Assert.Equal(result.SubjectId, notification.SubjectId);
        Assert.Equal("PRN227", notification.SubjectCode);
        Assert.Equal("Realtime Subject", notification.SubjectName);
        Assert.Equal(2, notification.TeacherIds.Count);
        Assert.Contains(teacherOneId, notification.TeacherIds);
        Assert.Contains(teacherTwoId, notification.TeacherIds);
    }

    [Fact]
    public async Task UpdateSubjectAsync_ReplacesAssignmentsAndHeader()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();

        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor Pages",
            Description = "Old description"
        });
        context.Teachers.AddRange(
            new Teacher
            {
                TeacherId = teacherOneId,
                FullName = "Alice Nguyen",
                Email = "alice@example.com"
            },
            new Teacher
            {
                TeacherId = teacherTwoId,
                FullName = "Bob Tran",
                Email = "bob@example.com"
            });
        context.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherSubjectId = Guid.NewGuid(),
            TeacherId = teacherOneId,
            SubjectId = subjectId,
            IsHeadOfDepartment = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.UpdateSubjectAsync(
            new UpdateSubjectRequest(
                subjectId,
                "PRN223",
                "Advanced Razor",
                "New description",
                [teacherTwoId],
                teacherTwoId));

        Assert.True(result.Succeeded);
        var subject = await context.Subjects.SingleAsync(item => item.SubjectId == subjectId);
        Assert.Equal("PRN223", subject.SubjectCode);
        Assert.Equal("Advanced Razor", subject.SubjectName);
        Assert.Equal("New description", subject.Description);

        var teacherSubjects = await context.TeacherSubjects
            .Where(item => item.SubjectId == subjectId)
            .ToListAsync();
        var teacherSubject = Assert.Single(teacherSubjects);
        Assert.Equal(teacherTwoId, teacherSubject.TeacherId);
        Assert.True(teacherSubject.IsHeadOfDepartment);
    }

    [Fact]
    public async Task UpdateSubjectAsync_NotifiesAddedRetainedAndRemovedTeachers()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();
        var teacherThreeId = Guid.NewGuid();
        var notifier = new RecordingTeacherSubjectRealtimeNotifier();

        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor Pages"
        });
        context.Teachers.AddRange(
            new Teacher
            {
                TeacherId = teacherOneId,
                FullName = "Alice Nguyen",
                Email = "alice@example.com"
            },
            new Teacher
            {
                TeacherId = teacherTwoId,
                FullName = "Bob Tran",
                Email = "bob@example.com"
            },
            new Teacher
            {
                TeacherId = teacherThreeId,
                FullName = "Carol Pham",
                Email = "carol@example.com"
            });
        context.TeacherSubjects.AddRange(
            new TeacherSubject
            {
                TeacherSubjectId = Guid.NewGuid(),
                TeacherId = teacherOneId,
                SubjectId = subjectId,
                IsHeadOfDepartment = true
            },
            new TeacherSubject
            {
                TeacherSubjectId = Guid.NewGuid(),
                TeacherId = teacherTwoId,
                SubjectId = subjectId,
                IsHeadOfDepartment = false
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, notifier);

        var result = await service.UpdateSubjectAsync(
            new UpdateSubjectRequest(
                subjectId,
                "PRN999",
                "Realtime Updated",
                null,
                [teacherTwoId, teacherThreeId],
                teacherTwoId));

        Assert.True(result.Succeeded);

        var assignedNotification = Assert.Single(notifier.AssignedNotifications);
        Assert.Equal(subjectId, assignedNotification.SubjectId);
        Assert.Equal("PRN999", assignedNotification.SubjectCode);
        Assert.Contains(teacherThreeId, assignedNotification.TeacherIds);
        Assert.Single(assignedNotification.TeacherIds);

        var updatedNotification = Assert.Single(notifier.UpdatedNotifications);
        Assert.Equal(subjectId, updatedNotification.SubjectId);
        Assert.Equal("Realtime Updated", updatedNotification.SubjectName);
        Assert.Contains(teacherTwoId, updatedNotification.TeacherIds);
        Assert.Single(updatedNotification.TeacherIds);

        var deletedNotification = Assert.Single(notifier.Notifications);
        Assert.Equal(subjectId, deletedNotification.SubjectId);
        Assert.Contains(teacherOneId, deletedNotification.TeacherIds);
        Assert.Single(deletedNotification.TeacherIds);
    }

    [Fact]
    public async Task GetTeacherSummariesAsync_ReturnsAllSubjectAssignments()
    {
        await using var context = CreateContext();
        var teacherId = Guid.NewGuid();
        var subjectOneId = Guid.NewGuid();
        var subjectTwoId = Guid.NewGuid();

        context.Teachers.Add(new Teacher
        {
            TeacherId = teacherId,
            FullName = "Alice Nguyen",
            Email = "alice@example.com",
            Department = "Software"
        });
        context.Subjects.AddRange(
            new Subject
            {
                SubjectId = subjectOneId,
                SubjectCode = "PRN222",
                SubjectName = "Razor Pages"
            },
            new Subject
            {
                SubjectId = subjectTwoId,
                SubjectCode = "SWD392",
                SubjectName = "Web API"
            });
        context.TeacherSubjects.AddRange(
            new TeacherSubject
            {
                TeacherSubjectId = Guid.NewGuid(),
                TeacherId = teacherId,
                SubjectId = subjectOneId,
                IsHeadOfDepartment = true
            },
            new TeacherSubject
            {
                TeacherSubjectId = Guid.NewGuid(),
                TeacherId = teacherId,
                SubjectId = subjectTwoId,
                IsHeadOfDepartment = false
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var summaries = await service.GetTeacherSummariesAsync();

        var summary = Assert.Single(summaries);
        Assert.Equal(2, summary.SubjectAssignments.Count);
        Assert.Contains("PRN222 - Razor Pages", summary.SubjectAssignments);
        Assert.Contains("SWD392 - Web API", summary.SubjectAssignments);
    }

    [Fact]
    public async Task DeleteSubjectAsync_DeletesAllRelatedSubjectData()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor Pages"
        });
        context.Teachers.Add(new Teacher
        {
            TeacherId = teacherId,
            FullName = "Alice Nguyen",
            Email = "alice@example.com",
            Department = "Software"
        });
        context.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherSubjectId = Guid.NewGuid(),
            TeacherId = teacherId,
            SubjectId = subjectId,
            IsHeadOfDepartment = true
        });
        context.Chapters.Add(new Chapter
        {
            ChapterId = chapterId,
            SubjectId = subjectId,
            ChapterTitle = "Routing"
        });
        context.TestQuestions.Add(new TestQuestion
        {
            QuestionId = questionId,
            ChapterId = chapterId,
            QuestionText = "What is a route?"
        });
        context.Documents.Add(new Document
        {
            DocumentId = documentId,
            ChapterId = chapterId,
            SubjectId = subjectId,
            Title = "Week 1 Slides",
            FileUrl = "/docs/week1.pdf",
            FileType = "pdf",
            ChunkingStrategy = DocumentChunkingStrategies.Recursive,
            ChunkSize = DocumentChunkingDefaults.RecursiveChunkSize,
            ChunkOverlap = DocumentChunkingDefaults.RecursiveChunkOverlap,
            Status = "completed"
        });
        context.Chunks.Add(new Chunk
        {
            ChunkId = chunkId,
            DocumentId = documentId,
            ChunkIndex = 0,
            Content = "Routing overview"
        });
        context.ProcessingJobs.Add(new ProcessingJob
        {
            JobId = Guid.NewGuid(),
            DocumentId = documentId,
            JobStatus = "queued"
        });
        context.Sessions.Add(new Session
        {
            SessionId = sessionId,
            UserId = Guid.NewGuid(),
            SubjectId = subjectId,
            StartedAt = DateTime.UtcNow
        });
        context.Messages.Add(new Message
        {
            MessageId = Guid.NewGuid(),
            SessionId = sessionId,
            SenderRole = "assistant",
            MessageContent = "Use route attributes.",
            CitationsJson = "[]"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.DeleteSubjectAsync(subjectId);

        Assert.True(result.Succeeded);
        Assert.Empty(await context.Subjects.ToListAsync());
        Assert.Empty(await context.TeacherSubjects.ToListAsync());
        Assert.Empty(await context.Chapters.ToListAsync());
        Assert.Empty(await context.TestQuestions.ToListAsync());
        Assert.Empty(await context.Documents.ToListAsync());
        Assert.Empty(await context.Chunks.ToListAsync());
        Assert.Empty(await context.ProcessingJobs.ToListAsync());
        Assert.Empty(await context.Sessions.ToListAsync());
        Assert.Empty(await context.Messages.ToListAsync());
        Assert.Single(await context.Teachers.ToListAsync());
    }

    [Fact]
    public async Task DeleteSubjectAsync_NotifiesAssignedTeachersAfterDeletion()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();
        var notifier = new RecordingTeacherSubjectRealtimeNotifier();

        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            SubjectCode = "PRN222",
            SubjectName = "Razor Pages"
        });
        context.Teachers.AddRange(
            new Teacher
            {
                TeacherId = teacherOneId,
                FullName = "Alice Nguyen",
                Email = "alice@example.com"
            },
            new Teacher
            {
                TeacherId = teacherTwoId,
                FullName = "Bob Tran",
                Email = "bob@example.com"
            });
        context.TeacherSubjects.AddRange(
            new TeacherSubject
            {
                TeacherSubjectId = Guid.NewGuid(),
                TeacherId = teacherOneId,
                SubjectId = subjectId,
                IsHeadOfDepartment = true
            },
            new TeacherSubject
            {
                TeacherSubjectId = Guid.NewGuid(),
                TeacherId = teacherTwoId,
                SubjectId = subjectId,
                IsHeadOfDepartment = false
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, notifier);

        var result = await service.DeleteSubjectAsync(subjectId);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notifier.Notifications);
        Assert.Equal(subjectId, notification.SubjectId);
        Assert.Equal("PRN222", notification.SubjectCode);
        Assert.Equal("Razor Pages", notification.SubjectName);
        Assert.Equal(2, notification.TeacherIds.Count);
        Assert.Contains(teacherOneId, notification.TeacherIds);
        Assert.Contains(teacherTwoId, notification.TeacherIds);
    }

    private static AdminUserService CreateService(
        AppDbContext context,
        ITeacherSubjectRealtimeNotifier? teacherSubjectRealtimeNotifier = null)
    {
        return new AdminUserService(
            new UnitOfWork(context),
            new PasswordHasher<User>(),
            new NoOpStudentCredentialEmailSender(),
            new NoOpTeacherCredentialEmailSender(),
            teacherSubjectRealtimeNotifier ?? new NoOpTeacherSubjectRealtimeNotifier(),
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

    private sealed class NoOpTeacherCredentialEmailSender : ITeacherCredentialEmailSender
    {
        public Task SendAsync(TeacherCredentialEmailRequest request, CancellationToken cancellationToken = default)
        {
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

    private sealed class RecordingTeacherSubjectRealtimeNotifier : ITeacherSubjectRealtimeNotifier
    {
        public List<TeacherSubjectAssignedNotification> AssignedNotifications { get; } = [];
        public List<TeacherSubjectUpdatedNotification> UpdatedNotifications { get; } = [];
        public List<TeacherSubjectDeletedNotification> Notifications { get; } = [];

        public Task NotifySubjectAssignedAsync(
            TeacherSubjectAssignedNotification notification,
            CancellationToken cancellationToken = default)
        {
            AssignedNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task NotifySubjectUpdatedAsync(
            TeacherSubjectUpdatedNotification notification,
            CancellationToken cancellationToken = default)
        {
            UpdatedNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task NotifySubjectDeletedAsync(
            TeacherSubjectDeletedNotification notification,
            CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
