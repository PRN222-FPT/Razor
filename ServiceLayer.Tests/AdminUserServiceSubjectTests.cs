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
    public async Task CreateSubjectAsync_FailsWhenHeaderTeacherIsMissing()
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

        Assert.False(result.Succeeded);
        Assert.Equal("Select a header teacher from the assigned teachers.", result.ErrorMessage);
        Assert.Empty(await context.Subjects.ToListAsync());
        Assert.Empty(await context.TeacherSubjects.ToListAsync());
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

    private static AdminUserService CreateService(AppDbContext context)
    {
        return new AdminUserService(
            new UnitOfWork(context),
            new PasswordHasher<User>(),
            new NoOpStudentCredentialEmailSender(),
            new NoOpTeacherCredentialEmailSender(),
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
}
