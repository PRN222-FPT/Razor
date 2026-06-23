using Microsoft.AspNetCore.Mvc;
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

    private sealed class RecordingAdminUserService : IAdminUserService
    {
        public int ResetAccountPasswordCalls { get; private set; }

        public UpdateAccountStatusResult ResetAccountPasswordResult { get; set; } = UpdateAccountStatusResult.Success();

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
            return Task.FromResult(CreateSubjectResult.Failure("Not implemented."));
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
}
