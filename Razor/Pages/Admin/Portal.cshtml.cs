using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Admin;

public class PortalModel(IAdminUserService adminUserService) : PageModel
{
    private const int PageSize = 3;
    private const long MaxStudentImportFileSizeBytes = 5 * 1024 * 1024;

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RoleFilter { get; set; }

    [BindProperty]
    public CreateTeacherInputModel NewTeacher { get; set; } = new();

    [BindProperty]
    public CreateSubjectInputModel NewSubject { get; set; } = new();

    [BindProperty]
    public UpdateSubjectInputModel UpdateSubject { get; set; } = new();

    [BindProperty]
    public ImportStudentsInputModel ImportStudents { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ImportErrorDetails { get; set; }

    public AdminUserManagementDto UserManagement { get; private set; } =
        new(0, 0, 0, 0, []);

    public IReadOnlyList<AdminSubjectSummaryDto> Subjects { get; private set; } =
        [];

    public IReadOnlyList<AdminTeacherSummaryDto> Teachers { get; private set; } =
        [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAdminStateAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateSubjectAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(NewSubject, nameof(NewSubject)))
        {
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        var result = await adminUserService.CreateSubjectAsync(
            new CreateSubjectRequest(
                NewSubject.SubjectCode,
                NewSubject.SubjectName,
                NewSubject.Description,
                NewSubject.AssignedTeacherIds,
                NewSubject.HeaderTeacherId),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The subject could not be created.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = $"Subject {NewSubject.SubjectCode.Trim().ToUpperInvariant()} was created.";

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = RoleFilter
        });
    }

    public async Task<IActionResult> OnPostDeleteSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await adminUserService.DeleteSubjectAsync(subjectId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The subject could not be deleted.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = "Subject and all related data were deleted.";

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = RoleFilter
        });
    }

    public async Task<IActionResult> OnPostUpdateSubjectAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(UpdateSubject, nameof(UpdateSubject)))
        {
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        var result = await adminUserService.UpdateSubjectAsync(
            new UpdateSubjectRequest(
                UpdateSubject.SubjectId,
                UpdateSubject.SubjectCode,
                UpdateSubject.SubjectName,
                UpdateSubject.Description,
                UpdateSubject.AssignedTeacherIds,
                UpdateSubject.HeaderTeacherId),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The subject could not be updated.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = $"Subject {UpdateSubject.SubjectCode.Trim().ToUpperInvariant()} was updated.";

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = RoleFilter
        });
    }

    public async Task<IActionResult> OnPostCreateTeacherAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(NewTeacher, nameof(NewTeacher)))
        {
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        var result = await adminUserService.CreateTeacherAsync(
            new CreateTeacherRequest(
                NewTeacher.Email,
                NewTeacher.SubjectId,
                NewTeacher.IsSubjectLeader),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The teacher account could not be created.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = $"Teacher account for {NewTeacher.Email.Trim().ToLowerInvariant()} was created.";

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = RoleFilter
        });
    }

    public async Task<IActionResult> OnPostSuspendAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await adminUserService.SuspendAccountAsync(userId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The account could not be suspended.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = "Account suspended.";

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = RoleFilter
        });
    }

    public async Task<IActionResult> OnPostReactivateAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await adminUserService.ReactivateAccountAsync(userId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The account could not be reactivated.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = "Account reactivated.";

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = RoleFilter
        });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await adminUserService.ResetAccountPasswordAsync(userId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The password could not be reset.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = "Password reset and sent by email.";

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = RoleFilter
        });
    }

    public async Task<IActionResult> OnPostImportStudentsAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(ImportStudents, nameof(ImportStudents)))
        {
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        if (ImportStudents.File!.Length > MaxStudentImportFileSizeBytes)
        {
            ModelState.AddModelError(string.Empty, "Student import file must be 5 MB or smaller.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        await using var stream = ImportStudents.File!.OpenReadStream();
        var result = await adminUserService.ImportStudentsAsync(
            new ImportStudentsRequest(
                ImportStudents.File.FileName,
                stream),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Students could not be imported.");
            await LoadAdminStateAsync(cancellationToken);
            return Page();
        }

        SuccessMessage = $"Created {result.CreatedCount} student account(s). Skipped {result.SkippedCount}.";
        ImportErrorDetails = result.Errors.Count == 0
            ? null
            : string.Join(Environment.NewLine, result.Errors);

        return RedirectToPage("/Admin/Portal", new
        {
            searchTerm = SearchTerm,
            roleFilter = "student"
        });
    }

    public bool IsRoleSelected(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return string.IsNullOrWhiteSpace(RoleFilter);
        }

        return string.Equals(RoleFilter, role, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetInitials(string fullName)
    {
        var nameParts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToArray();

        if (nameParts.Length == 0)
        {
            return "U";
        }

        return string.Concat(nameParts.Select(part => char.ToUpperInvariant(part[0])));
    }

    public static string FormatRole(string role)
    {
        var normalizedRole = string.IsNullOrWhiteSpace(role)
            ? "student"
            : role.Trim();

        return char.ToUpperInvariant(normalizedRole[0]) + normalizedRole[1..].ToLowerInvariant();
    }

    public static string GetRoleClass(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "admin" => "admin",
            "teacher" => "teacher",
            _ => "student"
        };
    }

    public string FormatSubjectOption(AdminSubjectSummaryDto subject)
    {
        return subject.HasLeader
            ? $"{subject.SubjectCode} - {subject.SubjectName} (header: {subject.LeaderName ?? "teacher"})"
            : $"{subject.SubjectCode} - {subject.SubjectName}";
    }

    public bool IsTeacherAssigned(AdminSubjectSummaryDto subject, Guid teacherId)
    {
        return subject.AssignedTeacherIds.Contains(teacherId);
    }

    public static string FormatSubjectStatus(AdminSubjectSummaryDto subject)
    {
        var teacherLabel = subject.AssignedTeacherCount == 1
            ? "1 teacher"
            : $"{subject.AssignedTeacherCount} teachers";

        return subject.HasLeader
            ? $"{teacherLabel}, header: {subject.LeaderName ?? "teacher"}"
            : $"{teacherLabel}, no header";
    }

    public static string FormatCreatedAt(DateTime? createdAt)
    {
        return createdAt.HasValue
            ? createdAt.Value.ToString("yyyy-MM-dd HH:mm")
            : "Not recorded";
    }

    public static string FormatAppliedDate(DateTime? createdAt)
    {
        return createdAt.HasValue
            ? createdAt.Value.ToString("MMM dd, yyyy")
            : "Not recorded";
    }

    private async Task LoadAdminStateAsync(CancellationToken cancellationToken)
    {
        UserManagement = await adminUserService.GetUserManagementAsync(
            SearchTerm,
            RoleFilter,
            PageSize,
            cancellationToken);

        Subjects = await adminUserService.GetSubjectSummariesAsync(cancellationToken);
        Teachers = await adminUserService.GetTeacherSummariesAsync(cancellationToken);
    }

    public IReadOnlyList<SelectListItem> HeaderTeacherOptions =>
        Teachers.Select(teacher => new SelectListItem
        {
            Value = teacher.TeacherId.ToString(),
            Text = $"{teacher.FullName} ({FormatTeacherAssignment(teacher)})",
            Selected = NewSubject.HeaderTeacherId == teacher.TeacherId
        })
            .ToList();

    public IReadOnlyList<SelectListItem> GetHeaderTeacherOptions(AdminSubjectSummaryDto subject) =>
        Teachers.Select(teacher => new SelectListItem
        {
            Value = teacher.TeacherId.ToString(),
            Text = $"{teacher.FullName} ({FormatTeacherAssignment(teacher)})",
            Selected = subject.HeaderTeacherId == teacher.TeacherId
        })
            .ToList();

    public sealed class CreateSubjectInputModel
    {
        [Required]
        [StringLength(50)]
        [Display(Name = "Subject code")]
        public string SubjectCode { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Display(Name = "Subject name")]
        public string SubjectName { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Assigned teachers")]
        public List<Guid> AssignedTeacherIds { get; set; } = [];

        [Display(Name = "Header teacher")]
        public Guid? HeaderTeacherId { get; set; }
    }

    public sealed class CreateTeacherInputModel
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Subject")]
        public Guid? SubjectId { get; set; }

        [Display(Name = "Leader of this subject")]
        public bool IsSubjectLeader { get; set; }
    }

    public sealed class UpdateSubjectInputModel
    {
        [Required]
        public Guid SubjectId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Subject code")]
        public string SubjectCode { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Display(Name = "Subject name")]
        public string SubjectName { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Assigned teachers")]
        public List<Guid> AssignedTeacherIds { get; set; } = [];

        [Display(Name = "Header teacher")]
        public Guid? HeaderTeacherId { get; set; }
    }

    public sealed class ImportStudentsInputModel
    {
        [Required]
        [Display(Name = "Google Sheets export")]
        public IFormFile? File { get; set; }
    }

    public static string FormatTeacherAssignment(AdminTeacherSummaryDto teacher)
    {
        return teacher.SubjectAssignments.Count == 0
            ? "Available"
            : $"Assigned to {string.Join(", ", teacher.SubjectAssignments)}";
    }
}
