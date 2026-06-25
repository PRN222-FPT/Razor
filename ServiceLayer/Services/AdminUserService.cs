using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class AdminUserService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IStudentCredentialEmailSender studentCredentialEmailSender,
    ITeacherCredentialEmailSender teacherCredentialEmailSender,
    ITeacherSubjectRealtimeNotifier teacherSubjectRealtimeNotifier,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    private const int MaxStudentImportRows = 1000;
    private const int GeneratedCredentialPasswordLength = 12;
    private static readonly char[] PasswordAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*?-_"
            .ToCharArray();

    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    private static readonly HashSet<string> SupportedRoleFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        "student",
        "teacher",
        "admin"
    };

    private static readonly HashSet<string> ManagedAccountRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "student",
        "teacher"
    };

    public async Task<AdminUserManagementDto> GetUserManagementAsync(
        string? searchTerm,
        string? roleFilter,
        int take = 12,
        CancellationToken cancellationToken = default)
    {
        var users = unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking();

        var normalizedSearch = searchTerm?.Trim().ToLowerInvariant();
        var normalizedRole = NormalizeRoleFilter(roleFilter);

        var totalUsers = await users.CountAsync(cancellationToken);
        var totalStudents = await users.CountAsync(
            user => user.Role != null && user.Role.ToLower() == "student",
            cancellationToken);
        var totalTeachers = await users.CountAsync(
            user => user.Role != null && user.Role.ToLower() == "teacher",
            cancellationToken);
        var activeUsers = await users.CountAsync(user => !user.IsBlocked, cancellationToken);

        var filteredUsers = users;
        if (!string.IsNullOrWhiteSpace(normalizedRole))
        {
            filteredUsers = filteredUsers.Where(
                user => user.Role != null && user.Role.ToLower() == normalizedRole);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filteredUsers = filteredUsers.Where(user =>
                user.FullName.ToLower().Contains(normalizedSearch)
                || user.Email.ToLower().Contains(normalizedSearch)
                || (user.StudentCode != null && user.StudentCode.ToLower().Contains(normalizedSearch)));
        }

        var recentUsers = await filteredUsers
            .OrderByDescending(user => user.CreatedAt ?? DateTime.MinValue)
            .ThenBy(user => user.FullName)
            .Take(Math.Clamp(take, 1, 50))
            .Select(user => new AdminUserSummaryDto(
                user.UserId,
                user.FullName,
                user.Email,
                user.Role ?? "student",
                user.StudentCode,
                user.IsBlocked,
                user.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminUserManagementDto(
            totalUsers,
            totalStudents,
            totalTeachers,
            activeUsers,
            recentUsers);
    }

    public async Task<IReadOnlyList<AdminSubjectSummaryDto>> GetSubjectSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Repository<Subject>()
            .Query()
            .AsNoTracking()
            .OrderBy(subject => subject.SubjectCode)
            .ThenBy(subject => subject.SubjectName)
            .Select(subject => new AdminSubjectSummaryDto(
                subject.SubjectId,
                subject.SubjectCode,
                subject.SubjectName,
                subject.Description,
                subject.TeacherSubjects.Count,
                subject.TeacherSubjects.Any(teacherSubject => teacherSubject.IsHeadOfDepartment),
                subject.TeacherSubjects
                    .Where(teacherSubject => teacherSubject.IsHeadOfDepartment)
                    .Select(teacherSubject => teacherSubject.Teacher.FullName)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminTeacherSummaryDto>> GetTeacherSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var teachers = await unitOfWork.Repository<Teacher>()
            .Query()
            .AsNoTracking()
            .Include(teacher => teacher.TeacherSubjects)
            .ThenInclude(teacherSubject => teacherSubject.Subject)
            .OrderBy(teacher => teacher.FullName)
            .ThenBy(teacher => teacher.Email)
            .ToListAsync(cancellationToken);

        return teachers
            .Select(teacher => new AdminTeacherSummaryDto(
                teacher.TeacherId,
                teacher.FullName,
                teacher.Email ?? string.Empty,
                teacher.Department ?? string.Empty,
                teacher.TeacherSubjects
                    .OrderBy(teacherSubject => teacherSubject.Subject.SubjectCode)
                    .ThenBy(teacherSubject => teacherSubject.Subject.SubjectName)
                    .Select(teacherSubject => $"{teacherSubject.Subject.SubjectCode} - {teacherSubject.Subject.SubjectName}")
                    .ToList()))
            .ToList();
    }

    public async Task<CreateSubjectResult> CreateSubjectAsync(
        CreateSubjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var subjectCode = request.SubjectCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var subjectName = request.SubjectName?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        var assignedTeacherIds = NormalizeTeacherIds(request.AssignedTeacherIds);
        var headerTeacherId = request.HeaderTeacherId;

        if (string.IsNullOrWhiteSpace(subjectCode))
        {
            return CreateSubjectResult.Failure("Subject code is required.");
        }

        if (string.IsNullOrWhiteSpace(subjectName))
        {
            return CreateSubjectResult.Failure("Subject name is required.");
        }

        var subjects = unitOfWork.Repository<Subject>();
        var subjectCodeExists = await subjects.Query()
            .AsNoTracking()
            .AnyAsync(subject => subject.SubjectCode.ToLower() == subjectCode.ToLower(), cancellationToken);
        if (subjectCodeExists)
        {
            return CreateSubjectResult.Failure("A subject with this code already exists.");
        }

        if (headerTeacherId.HasValue && !assignedTeacherIds.Contains(headerTeacherId.Value))
        {
            assignedTeacherIds.Add(headerTeacherId.Value);
        }

        if (assignedTeacherIds.Count > 0)
        {
            var existingTeacherIds = await unitOfWork.Repository<Teacher>()
                .Query()
                .AsNoTracking()
                .Where(teacher => assignedTeacherIds.Contains(teacher.TeacherId))
                .Select(teacher => teacher.TeacherId)
                .ToListAsync(cancellationToken);

            if (existingTeacherIds.Count != assignedTeacherIds.Count)
            {
                return CreateSubjectResult.Failure("One or more selected teachers do not exist.");
            }
        }

        var subject = new Subject
        {
            SubjectId = Guid.NewGuid(),
            SubjectCode = subjectCode,
            SubjectName = subjectName,
            Description = description
        };

        await subjects.AddAsync(subject, cancellationToken);
        if (assignedTeacherIds.Count > 0)
        {
            var teacherSubjects = unitOfWork.Repository<TeacherSubject>();
            foreach (var teacherId in assignedTeacherIds)
            {
                await teacherSubjects.AddAsync(
                    new TeacherSubject
                    {
                        TeacherSubjectId = Guid.NewGuid(),
                        TeacherId = teacherId,
                        SubjectId = subject.SubjectId,
                        IsHeadOfDepartment = headerTeacherId.HasValue
                            && teacherId == headerTeacherId.Value
                    },
                    cancellationToken);
            }
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "subjects_subject_code_key"))
        {
            return CreateSubjectResult.Failure("A subject with this code already exists.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "teacher_subjects_one_leader_per_subject"))
        {
            return CreateSubjectResult.Failure("The selected subject already has a header teacher.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "teacher_subjects_teacher_id_key"))
        {
            return CreateSubjectResult.Failure(
                "The database still has the old one-subject-per-teacher constraint. Restart the app so the compatibility update can run, then try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "teacher_subjects_subject_id_key"))
        {
            return CreateSubjectResult.Failure(
                "The database still has the old one-teacher-per-subject constraint. Restart the app so the compatibility update can run, then try again.");
        }

        if (assignedTeacherIds.Count > 0)
        {
            await teacherSubjectRealtimeNotifier.NotifySubjectAssignedAsync(
                new TeacherSubjectAssignedNotification(
                    subject.SubjectId,
                    subject.SubjectCode,
                    subject.SubjectName,
                    assignedTeacherIds),
                cancellationToken);
        }

        return CreateSubjectResult.Success(subject.SubjectId);
    }

    public async Task<DeleteSubjectResult> DeleteSubjectAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var subject = await unitOfWork.Repository<Subject>()
            .Query()
            .SingleOrDefaultAsync(item => item.SubjectId == subjectId, cancellationToken);
        if (subject is null)
        {
            return DeleteSubjectResult.Failure("The subject was not found.");
        }

        var teacherSubjects = await unitOfWork.Repository<TeacherSubject>()
            .Query()
            .Where(item => item.SubjectId == subjectId)
            .ToListAsync(cancellationToken);
        var affectedTeacherIds = teacherSubjects
            .Select(item => item.TeacherId)
            .Distinct()
            .ToArray();

        var sessions = await unitOfWork.Repository<Session>()
            .Query()
            .Where(item => item.SubjectId == subjectId)
            .ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(item => item.SessionId).ToList();

        var messages = sessionIds.Count == 0
            ? new List<Message>()
            : await unitOfWork.Repository<Message>()
                .Query()
                .Where(item => sessionIds.Contains(item.SessionId))
                .ToListAsync(cancellationToken);

        var chapters = await unitOfWork.Repository<Chapter>()
            .Query()
            .Where(item => item.SubjectId == subjectId)
            .ToListAsync(cancellationToken);
        var chapterIds = chapters.Select(item => item.ChapterId).ToList();

        var testQuestions = chapterIds.Count == 0
            ? new List<TestQuestion>()
            : await unitOfWork.Repository<TestQuestion>()
                .Query()
                .Where(item => chapterIds.Contains(item.ChapterId))
                .ToListAsync(cancellationToken);

        var documents = await unitOfWork.Repository<Document>()
            .Query()
            .Where(item => item.SubjectId == subjectId)
            .ToListAsync(cancellationToken);
        var documentIds = documents.Select(item => item.DocumentId).ToList();

        var chunks = documentIds.Count == 0
            ? new List<Chunk>()
            : await unitOfWork.Repository<Chunk>()
                .Query()
                .Where(item => documentIds.Contains(item.DocumentId))
                .ToListAsync(cancellationToken);

        var processingJobs = documentIds.Count == 0
            ? new List<ProcessingJob>()
            : await unitOfWork.Repository<ProcessingJob>()
                .Query()
                .Where(item => documentIds.Contains(item.DocumentId))
                .ToListAsync(cancellationToken);

        var messagesRepo = unitOfWork.Repository<Message>();
        foreach (var message in messages)
        {
            messagesRepo.Delete(message);
        }

        var sessionsRepo = unitOfWork.Repository<Session>();
        foreach (var session in sessions)
        {
            sessionsRepo.Delete(session);
        }

        var testQuestionRepo = unitOfWork.Repository<TestQuestion>();
        foreach (var testQuestion in testQuestions)
        {
            testQuestionRepo.Delete(testQuestion);
        }

        var processingJobRepo = unitOfWork.Repository<ProcessingJob>();
        foreach (var processingJob in processingJobs)
        {
            processingJobRepo.Delete(processingJob);
        }

        var chunkRepo = unitOfWork.Repository<Chunk>();
        foreach (var chunk in chunks)
        {
            chunkRepo.Delete(chunk);
        }

        var documentRepo = unitOfWork.Repository<Document>();
        foreach (var document in documents)
        {
            documentRepo.Delete(document);
        }

        var chapterRepo = unitOfWork.Repository<Chapter>();
        foreach (var chapter in chapters)
        {
            chapterRepo.Delete(chapter);
        }

        var teacherSubjectRepo = unitOfWork.Repository<TeacherSubject>();
        foreach (var teacherSubject in teacherSubjects)
        {
            teacherSubjectRepo.Delete(teacherSubject);
        }

        unitOfWork.Repository<Subject>().Delete(subject);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (affectedTeacherIds.Length > 0)
        {
            await teacherSubjectRealtimeNotifier.NotifySubjectDeletedAsync(
                new TeacherSubjectDeletedNotification(
                    subject.SubjectId,
                    subject.SubjectCode,
                    subject.SubjectName,
                    affectedTeacherIds),
                cancellationToken);
        }

        logger.LogInformation(
            "Subject deleted. SubjectId={SubjectId}, TeacherAssignments={TeacherAssignments}, Sessions={SessionCount}, Documents={DocumentCount}",
            subjectId,
            teacherSubjects.Count,
            sessions.Count,
            documents.Count);

        return DeleteSubjectResult.Success();
    }

    public async Task<CreateTeacherResult> CreateTeacherAsync(
        CreateTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
        {
            return CreateTeacherResult.Failure("Email is required.");
        }

        if (!EmailAddressValidator.IsValid(email))
        {
            return CreateTeacherResult.Failure("Email must be a valid email address.");
        }

        if (request.IsSubjectLeader && !request.SubjectId.HasValue)
        {
            return CreateTeacherResult.Failure("Select a subject before marking the teacher as leader.");
        }

        var users = unitOfWork.Repository<User>();
        var teachers = unitOfWork.Repository<Teacher>();
        var teacherSubjects = unitOfWork.Repository<TeacherSubject>();

        var userEmailExists = await users.Query()
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);
        if (userEmailExists)
        {
            return CreateTeacherResult.Failure("A user account with this email already exists.");
        }

        var teacherEmailExists = await teachers.Query()
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Email != null && candidate.Email.ToLower() == email, cancellationToken);
        if (teacherEmailExists)
        {
            return CreateTeacherResult.Failure("A teacher profile with this email already exists.");
        }

        if (request.SubjectId.HasValue)
        {
            var subjectExists = await unitOfWork.Repository<Subject>()
                .Query()
                .AsNoTracking()
                .AnyAsync(subject => subject.SubjectId == request.SubjectId.Value, cancellationToken);
            if (!subjectExists)
            {
                return CreateTeacherResult.Failure("The selected subject does not exist.");
            }

            var subjectAlreadyHasLeader = await teacherSubjects.Query()
                .AsNoTracking()
                .AnyAsync(
                    teacherSubject => teacherSubject.SubjectId == request.SubjectId.Value
                        && teacherSubject.IsHeadOfDepartment,
                    cancellationToken);
            if (request.IsSubjectLeader && subjectAlreadyHasLeader)
            {
                return CreateTeacherResult.Failure("The selected subject already has a leader.");
            }
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var displayName = BuildTeacherDisplayName(email);
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = displayName,
            Email = email,
            Role = "teacher",
            IsBlocked = false,
            StudentCode = null
        };
        user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);

        var teacher = new Teacher
        {
            TeacherId = Guid.NewGuid(),
            FullName = displayName,
            Email = email,
            Department = null
        };

        await users.AddAsync(user, cancellationToken);
        await teachers.AddAsync(teacher, cancellationToken);

        TeacherSubject? teacherSubject = null;
        if (request.SubjectId.HasValue)
        {
            teacherSubject = new TeacherSubject
            {
                TeacherSubjectId = Guid.NewGuid(),
                TeacherId = teacher.TeacherId,
                SubjectId = request.SubjectId.Value,
                IsHeadOfDepartment = request.IsSubjectLeader
            };

            await teacherSubjects.AddAsync(teacherSubject, cancellationToken);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "teacher_subjects_one_leader_per_subject"))
        {
            return CreateTeacherResult.Failure("The selected subject already has a leader.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "teacher_subjects_teacher_id_key"))
        {
            return CreateTeacherResult.Failure(
                "The database still has the old one-subject-per-teacher constraint. Restart the app so the compatibility update can run, then try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "teacher_subjects_subject_id_key"))
        {
            return CreateTeacherResult.Failure("The database still has the old one-teacher-per-subject constraint. Restart the app so the compatibility update can run, then try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "users_email_key")
            || IsUniqueConstraintViolation(ex, "teachers_email_key"))
        {
            return CreateTeacherResult.Failure("A teacher account with this email already exists.");
        }

        try
        {
            await teacherCredentialEmailSender.SendAsync(
                new TeacherCredentialEmailRequest(
                    displayName,
                    email,
                    temporaryPassword),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send teacher credential email. Rolling back the account.");

            if (teacherSubject is not null)
            {
                teacherSubjects.Delete(teacherSubject);
            }

            teachers.Delete(teacher);
            users.Delete(user);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(
                    rollbackEx,
                    "Failed to roll back teacher account after email delivery failed.");

                return CreateTeacherResult.Failure(
                    "A teacher account was created but the credentials email failed and the rollback could not be completed.");
            }

            return CreateTeacherResult.Failure("The teacher account was created but the credentials email could not be sent.");
        }

        return CreateTeacherResult.Success(user.UserId);
    }

    public async Task<UpdateAccountStatusResult> SuspendAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Repository<User>()
            .Query()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (user is null)
        {
            return UpdateAccountStatusResult.Failure("The account was not found.");
        }

        if (!CanManageAccount(user))
        {
            return UpdateAccountStatusResult.Failure("Only student and teacher accounts can be suspended.");
        }

        if (user.IsBlocked)
        {
            return UpdateAccountStatusResult.Failure("The account is already suspended.");
        }

        user.IsBlocked = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UpdateAccountStatusResult.Success();
    }

    public async Task<UpdateAccountStatusResult> ReactivateAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Repository<User>()
            .Query()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (user is null)
        {
            return UpdateAccountStatusResult.Failure("The account was not found.");
        }

        if (!CanManageAccount(user))
        {
            return UpdateAccountStatusResult.Failure("Only student and teacher accounts can be reactivated.");
        }

        if (!user.IsBlocked)
        {
            return UpdateAccountStatusResult.Failure("The account is already active.");
        }

        user.IsBlocked = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UpdateAccountStatusResult.Success();
    }

    public async Task<UpdateAccountStatusResult> ResetAccountPasswordAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await unitOfWork.Repository<User>()
            .Query()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (user is null)
        {
            return UpdateAccountStatusResult.Failure("The account was not found.");
        }

        if (!CanManageAccount(user))
        {
            return UpdateAccountStatusResult.Failure("Only student and teacher accounts can have their passwords reset.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return UpdateAccountStatusResult.Failure("The account does not have a password to reset.");
        }

        var previousPasswordHash = user.PasswordHash;
        var previousResetTokenHash = user.PasswordResetTokenHash;
        var previousResetTokenExpiresAt = user.PasswordResetTokenExpiresAt;
        var temporaryPassword = GenerateTemporaryPassword();

        user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);
        ClearPasswordResetState(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await SendPasswordResetCredentialsAsync(user, temporaryPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send admin password reset email. Rolling back the password update. UserId={UserId}",
                userId);

            user.PasswordHash = previousPasswordHash;
            user.PasswordResetTokenHash = previousResetTokenHash;
            user.PasswordResetTokenExpiresAt = previousResetTokenExpiresAt;

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(
                    rollbackEx,
                    "Failed to roll back admin password reset. UserId={UserId}",
                    userId);

                return UpdateAccountStatusResult.Failure(
                    "The password was updated but the email failed and the rollback could not be completed.");
            }

            return UpdateAccountStatusResult.Failure("The password was updated but the notification email could not be sent.");
        }

        return UpdateAccountStatusResult.Success();
    }

    public async Task<ImportStudentsResult> ImportStudentsAsync(
        ImportStudentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        if (extension is not ".xlsx" and not ".csv")
        {
            return ImportStudentsResult.Failure("Upload a .xlsx or .csv file exported from Google Sheets.");
        }

        List<StudentImportRawRow> rows;
        try
        {
            rows = extension == ".xlsx"
                ? ReadXlsxRows(request.Content)
                : await ReadCsvRowsAsync(request.Content, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            return ImportStudentsResult.Failure(ex.Message);
        }

        if (rows.Count == 0)
        {
            return ImportStudentsResult.Failure("The file does not contain any student rows.");
        }

        if (rows.Count > MaxStudentImportRows)
        {
            return ImportStudentsResult.Failure($"Import files can contain at most {MaxStudentImportRows} students.");
        }

        var errors = new List<string>();
        var validRows = ValidateStudentRows(rows, errors);
        if (validRows.Count == 0)
        {
            return ImportStudentsResult.Success(0, rows.Count, errors);
        }

        var normalizedEmails = validRows
            .Select(row => row.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalizedStudentCodes = validRows
            .Select(row => row.StudentCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingUsers = await unitOfWork.Repository<User>()
            .Query()
            .AsNoTracking()
            .Where(user => normalizedEmails.Contains(user.Email.ToLower())
                || (user.StudentCode != null && normalizedStudentCodes.Contains(user.StudentCode.ToLower())))
            .Select(user => new
            {
                Email = user.Email.ToLower(),
                StudentCode = user.StudentCode == null ? null : user.StudentCode.ToLower()
            })
            .ToListAsync(cancellationToken);
        var existingEmails = existingUsers
            .Select(user => user.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingStudentCodes = existingUsers
            .Where(user => user.StudentCode is not null)
            .Select(user => user.StudentCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var createdCount = 0;
        var users = unitOfWork.Repository<User>();
        foreach (var row in validRows)
        {
            if (existingEmails.Contains(row.Email))
            {
                errors.Add($"Row {row.RowNumber}: email already exists.");
                continue;
            }

            if (existingStudentCodes.Contains(row.StudentCode))
            {
                errors.Add($"Row {row.RowNumber}: MSSV already exists.");
                continue;
            }

            var temporaryPassword = GenerateTemporaryPassword();
            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = row.FullName,
                Email = row.Email,
                Role = "student",
                StudentCode = row.StudentCode,
                IsBlocked = false,
                CreatedAt = CurrentTimestamp()
            };
            user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);

            await users.AddAsync(user, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, "users_email_key")
                || IsUniqueConstraintViolation(ex, "users_student_code_key"))
            {
                users.Delete(user);
                errors.Add($"Row {row.RowNumber}: email or MSSV already exists.");
                continue;
            }

            try
            {
                await studentCredentialEmailSender.SendAsync(
                    new StudentCredentialEmailRequest(
                        user.FullName,
                        user.Email,
                        user.StudentCode ?? string.Empty,
                        temporaryPassword),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to send student credential email for row {RowNumber}. Rolling back the account.",
                    row.RowNumber);

                users.Delete(user);
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    logger.LogError(
                        rollbackEx,
                        "Failed to roll back student account after email delivery failed for row {RowNumber}.",
                        row.RowNumber);

                    return ImportStudentsResult.Failure(
                        "A student account was created but the credentials email failed and the rollback could not be completed.");
                }

                errors.Add($"Row {row.RowNumber}: credentials email could not be sent.");
                continue;
            }

            existingEmails.Add(row.Email);
            existingStudentCodes.Add(row.StudentCode);
            createdCount++;
        }

        return ImportStudentsResult.Success(
            createdCount,
            rows.Count - createdCount,
            errors.Take(20).ToList());
    }

    private async Task SendPasswordResetCredentialsAsync(
        User user,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        if (string.Equals(user.Role, "student", StringComparison.OrdinalIgnoreCase))
        {
            await studentCredentialEmailSender.SendAsync(
                new StudentCredentialEmailRequest(
                    user.FullName,
                    user.Email,
                    user.StudentCode ?? string.Empty,
                    temporaryPassword),
                cancellationToken);

            return;
        }

        await teacherCredentialEmailSender.SendAsync(
            new TeacherCredentialEmailRequest(
                user.FullName,
                user.Email,
                temporaryPassword),
            cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception, string constraintName)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeRoleFilter(string? roleFilter)
    {
        var normalizedRole = roleFilter?.Trim().ToLowerInvariant();

        return string.IsNullOrWhiteSpace(normalizedRole) || !SupportedRoleFilters.Contains(normalizedRole)
            ? null
            : normalizedRole;
    }

    private static List<Guid> NormalizeTeacherIds(IReadOnlyList<Guid>? teacherIds)
    {
        if (teacherIds is null || teacherIds.Count == 0)
        {
            return [];
        }

        return teacherIds
            .Where(teacherId => teacherId != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static bool CanManageAccount(User user)
    {
        return !string.IsNullOrWhiteSpace(user.Role)
            && ManagedAccountRoles.Contains(user.Role.Trim());
    }

    private static void ClearPasswordResetState(User user)
    {
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
    }

    private static string BuildTeacherDisplayName(string email)
    {
        var localPart = email.Split('@')[0];
        var cleaned = localPart
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('+', ' ');
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return email;
        }

        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        return string.Join(' ', words.Select(word => textInfo.ToTitleCase(word.ToLowerInvariant())));
    }

    private static List<StudentImportRow> ValidateStudentRows(
        IReadOnlyList<StudentImportRawRow> rows,
        List<string> errors)
    {
        var validRows = new List<StudentImportRow>();
        var fileEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var studentCode = row.StudentCode.Trim().ToLowerInvariant();
            var email = row.Email.Trim().ToLowerInvariant();
            var fullName = NormalizeName(row.FullName);

            if (string.IsNullOrWhiteSpace(studentCode))
            {
                errors.Add($"Row {row.RowNumber}: MSSV is required.");
                continue;
            }

            if (studentCode.Length > 50)
            {
                errors.Add($"Row {row.RowNumber}: MSSV must be 50 characters or fewer.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add($"Row {row.RowNumber}: email is required.");
                continue;
            }

            if (email.Length > 255 || !EmailAddressValidator.IsValid(email))
            {
                errors.Add($"Row {row.RowNumber}: email is invalid.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                errors.Add($"Row {row.RowNumber}: name is required.");
                continue;
            }

            if (fullName.Length > 255)
            {
                errors.Add($"Row {row.RowNumber}: name must be 255 characters or fewer.");
                continue;
            }

            if (!fileStudentCodes.Add(studentCode))
            {
                errors.Add($"Row {row.RowNumber}: duplicate MSSV in file.");
                continue;
            }

            if (!fileEmails.Add(email))
            {
                errors.Add($"Row {row.RowNumber}: duplicate email in file.");
                continue;
            }

            validRows.Add(new StudentImportRow(row.RowNumber, studentCode, email, fullName));
        }

        return validRows;
    }

    private static string GenerateTemporaryPassword()
    {
        Span<char> password = stackalloc char[GeneratedCredentialPasswordLength];
        var randomBytes = RandomNumberGenerator.GetBytes(GeneratedCredentialPasswordLength);
        for (var index = 0; index < password.Length; index++)
        {
            password[index] = PasswordAlphabet[randomBytes[index] % PasswordAlphabet.Length];
        }

        password[0] = GetRandomCharacter("ABCDEFGHJKLMNPQRSTUVWXYZ");
        password[1] = GetRandomCharacter("abcdefghijkmnopqrstuvwxyz");
        password[2] = GetRandomCharacter("23456789");
        password[3] = GetRandomCharacter("!@#$%*?-_");

        Shuffle(password);
        return new string(password);
    }

    private static char GetRandomCharacter(string characters)
    {
        return characters[RandomNumberGenerator.GetInt32(characters.Length)];
    }

    private static void Shuffle(Span<char> characters)
    {
        for (var index = characters.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            if (swapIndex == index)
            {
                continue;
            }

            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }
    }

    private static List<StudentImportRawRow> ReadXlsxRows(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The spreadsheet workbook is missing.");
        var worksheetPart = workbookPart.WorksheetParts.FirstOrDefault()
            ?? throw new InvalidDataException("The spreadsheet does not contain a worksheet.");
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException("The spreadsheet worksheet is empty.");
        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidDataException("The spreadsheet worksheet is empty.");

        var rows = sheetData.Elements<Row>().ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        var headerCells = rows[0].Elements<Cell>().ToList();
        var headerMap = BuildHeaderMap(headerCells.Select(cell => GetCellValue(workbookPart, cell)).ToList());

        return rows
            .Skip(1)
            .Select(row =>
            {
                var values = row.Elements<Cell>()
                    .ToDictionary(
                        cell => GetColumnIndex(cell.CellReference?.Value),
                        cell => GetCellValue(workbookPart, cell));

                return new StudentImportRawRow(
                    (int)(row.RowIndex?.Value ?? 0),
                    GetValue(values, headerMap["mssv"]),
                    GetValue(values, headerMap["email"]),
                    GetValue(values, headerMap["name"]));
            })
            .Where(row => !row.IsBlank)
            .ToList();
    }

    private static async Task<List<StudentImportRawRow>> ReadCsvRowsAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
        var lines = new List<string>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            return [];
        }

        var headerMap = BuildHeaderMap(ParseCsvLine(lines[0]));
        return lines
            .Skip(1)
            .Select((line, index) =>
            {
                var values = ParseCsvLine(line);

                return new StudentImportRawRow(
                    index + 2,
                    GetValue(values, headerMap["mssv"]),
                    GetValue(values, headerMap["email"]),
                    GetValue(values, headerMap["name"]));
            })
            .Where(row => !row.IsBlank)
            .ToList();
    }

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var map = headers
            .Select((header, index) => new
            {
                Header = NormalizeHeader(header),
                Index = index
            })
            .Where(item => item.Header.Length > 0)
            .GroupBy(item => item.Header)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        foreach (var requiredHeader in new[] { "mssv", "email", "name" })
        {
            if (!map.ContainsKey(requiredHeader))
            {
                throw new InvalidDataException("The file must include headers: MSSV, email, name.");
            }
        }

        return map;
    }

    private static string GetCellValue(WorkbookPart workbookPart, Cell cell)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }

        var value = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value != CellValues.SharedString)
        {
            return value;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex))
        {
            return string.Empty;
        }

        var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

        return sharedStringTable?
            .Elements<SharedStringItem>()
            .ElementAtOrDefault(sharedStringIndex)?
            .InnerText ?? string.Empty;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }

        var index = 0;
        foreach (var character in cellReference.Where(char.IsLetter))
        {
            index *= 26;
            index += char.ToUpperInvariant(character) - 'A' + 1;
        }

        return Math.Max(0, index - 1);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());

        return values;
    }

    private static string GetValue(IReadOnlyList<string> values, int index)
    {
        return index >= 0 && index < values.Count
            ? values[index]
            : string.Empty;
    }

    private static string GetValue(Dictionary<int, string> values, int index)
    {
        return values.TryGetValue(index, out var value)
            ? value
            : string.Empty;
    }

    private static string NormalizeHeader(string header)
    {
        return header.Trim().ToLowerInvariant().Replace(" ", string.Empty);
    }

    private static string NormalizeName(string name)
    {
        return string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static DateTime CurrentTimestamp()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    private sealed record StudentImportRawRow(
        int RowNumber,
        string StudentCode,
        string Email,
        string FullName)
    {
        public bool IsBlank =>
            string.IsNullOrWhiteSpace(StudentCode)
            && string.IsNullOrWhiteSpace(Email)
            && string.IsNullOrWhiteSpace(FullName);
    }

    private sealed record StudentImportRow(
        int RowNumber,
        string StudentCode,
        string Email,
        string FullName);
}
