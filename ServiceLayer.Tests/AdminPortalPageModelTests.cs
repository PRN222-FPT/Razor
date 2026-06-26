using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Razor.Pages.Admin;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class AdminPortalPageModelTests
{
    [Fact]
    public async Task OnPostResetPasswordAsync_RedirectsAfterSuccess()
    {
        var service = new RecordingAdminUserService
        {
            ResetAccountPasswordResult = UpdateAccountStatusResult.Success()
        };
        var model = new PortalModel(service);

        var result = await model.OnPostResetPasswordAsync(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Portal", redirect.PageName);
        Assert.Equal(1, service.ResetAccountPasswordCalls);
    }

    [Fact]
    public async Task OnPostCreateSubjectAsync_ForwardsHeaderTeacherSelection()
    {
        var service = new RecordingAdminUserService
        {
            CreateSubjectResponse = CreateSubjectResult.Success(Guid.NewGuid())
        };
        var model = new PortalModel(service)
        {
            NewSubject =
            {
                SubjectCode = "PRN222",
                SubjectName = "Razor Pages",
                Description = "Web development",
                AssignedTeacherIds = [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")],
                HeaderTeacherId = Guid.Parse("22222222-2222-2222-2222-222222222222")
            }
        };
        model.PageContext = CreatePageContext();

        var result = await model.OnPostCreateSubjectAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Portal", redirect.PageName);
        Assert.Equal("PRN222", service.LastCreateSubjectRequest?.SubjectCode);
        Assert.Equal("Razor Pages", service.LastCreateSubjectRequest?.SubjectName);
        Assert.Equal("Web development", service.LastCreateSubjectRequest?.Description);
        Assert.Equal(2, service.LastCreateSubjectRequest?.AssignedTeacherIds.Count);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), service.LastCreateSubjectRequest?.HeaderTeacherId);
        Assert.Equal(1, service.CreateSubjectCalls);
    }

    [Fact]
    public async Task OnPostCreateSubjectAsync_AllowsEmptyAssignments()
    {
        var service = new RecordingAdminUserService
        {
            CreateSubjectResponse = CreateSubjectResult.Success(Guid.NewGuid())
        };
        var model = new PortalModel(service)
        {
            NewSubject =
            {
                SubjectCode = "PRN226",
                SubjectName = "Razor Pages",
                Description = null,
                AssignedTeacherIds = [],
                HeaderTeacherId = null
            }
        };
        model.PageContext = CreatePageContext();

        var result = await model.OnPostCreateSubjectAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Portal", redirect.PageName);
        Assert.Equal("PRN226", service.LastCreateSubjectRequest?.SubjectCode);
        Assert.Empty(service.LastCreateSubjectRequest?.AssignedTeacherIds ?? []);
        Assert.Null(service.LastCreateSubjectRequest?.HeaderTeacherId);
    }

    [Fact]
    public async Task OnPostUpdateSubjectAsync_ForwardsUpdatePayload()
    {
        var service = new RecordingAdminUserService
        {
            UpdateSubjectResponse = UpdateSubjectResult.Success()
        };
        var subjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var model = new PortalModel(service)
        {
            UpdateSubject =
            {
                SubjectId = subjectId,
                SubjectCode = "PRN223",
                SubjectName = "Advanced Razor",
                Description = "Updated description",
                AssignedTeacherIds = [Guid.Parse("11111111-1111-1111-1111-111111111111")],
                HeaderTeacherId = Guid.Parse("11111111-1111-1111-1111-111111111111")
            }
        };
        model.PageContext = CreatePageContext();

        var result = await model.OnPostUpdateSubjectAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Portal", redirect.PageName);
        Assert.Equal(subjectId, service.LastUpdateSubjectRequest?.SubjectId);
        Assert.Equal("PRN223", service.LastUpdateSubjectRequest?.SubjectCode);
        Assert.Equal("Advanced Razor", service.LastUpdateSubjectRequest?.SubjectName);
        Assert.Equal("Updated description", service.LastUpdateSubjectRequest?.Description);
        Assert.Single(service.LastUpdateSubjectRequest?.AssignedTeacherIds ?? []);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), service.LastUpdateSubjectRequest?.HeaderTeacherId);
        Assert.Equal(1, service.UpdateSubjectCalls);
    }

    private sealed class RecordingAdminUserService : IAdminUserService
    {
        public int ResetAccountPasswordCalls { get; private set; }

        public int CreateSubjectCalls { get; private set; }

        public int UpdateSubjectCalls { get; private set; }

        public CreateSubjectRequest? LastCreateSubjectRequest { get; private set; }

        public UpdateSubjectRequest? LastUpdateSubjectRequest { get; private set; }

        public UpdateAccountStatusResult ResetAccountPasswordResult { get; set; } = UpdateAccountStatusResult.Success();

        public CreateSubjectResult CreateSubjectResponse { get; set; } = CreateSubjectResult.Failure("Not implemented.");

        public UpdateSubjectResult UpdateSubjectResponse { get; set; } = UpdateSubjectResult.Failure("Not implemented.");

        public Task<AdminUserManagementDto> GetUserManagementAsync(
            string? searchTerm,
            string? roleFilter,
            int take = 12,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AdminUserManagementDto(0, 0, 0, 0, []));
        }

        public Task<IReadOnlyList<AdminSubjectSummaryDto>> GetSubjectSummariesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AdminSubjectSummaryDto>>([]);
        }

        public Task<IReadOnlyList<AdminTeacherSummaryDto>> GetTeacherSummariesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AdminTeacherSummaryDto>>([]);
        }

        public Task<CreateSubjectResult> CreateSubjectAsync(
            CreateSubjectRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateSubjectCalls++;
            LastCreateSubjectRequest = request;
            return Task.FromResult(CreateSubjectResponse);
        }

        public Task<UpdateSubjectResult> UpdateSubjectAsync(
            UpdateSubjectRequest request,
            CancellationToken cancellationToken = default)
        {
            UpdateSubjectCalls++;
            LastUpdateSubjectRequest = request;
            return Task.FromResult(UpdateSubjectResponse);
        }

        public Task<DeleteSubjectResult> DeleteSubjectAsync(
            Guid subjectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteSubjectResult.Failure("Not implemented."));
        }

        public Task<CreateTeacherResult> CreateTeacherAsync(
            CreateTeacherRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateTeacherResult.Failure("Not implemented."));
        }

        public Task<UpdateAccountStatusResult> SuspendAccountAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UpdateAccountStatusResult.Failure("Not implemented."));
        }

        public Task<UpdateAccountStatusResult> ReactivateAccountAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UpdateAccountStatusResult.Failure("Not implemented."));
        }

        public Task<UpdateAccountStatusResult> ResetAccountPasswordAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            ResetAccountPasswordCalls++;
            return Task.FromResult(ResetAccountPasswordResult);
        }

        public Task<ImportStudentsResult> ImportStudentsAsync(
            ImportStudentsRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ImportStudentsResult.Failure("Not implemented."));
        }
    }

    private static PageContext CreatePageContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddMvcCore()
            .AddDataAnnotations()
            .Services
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        return new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));
    }
}
