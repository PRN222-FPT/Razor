using System.ComponentModel.DataAnnotations;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class DefaultStudentSeeder(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IOptions<DefaultStudentOptions> options) : IDefaultStudentSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var defaultStudent = options.Value;
        if (!defaultStudent.Enabled)
        {
            return;
        }

        Validate(defaultStudent);

        var email = defaultStudent.Email.Trim().ToLowerInvariant();
        var studentCode = defaultStudent.StudentCode.Trim().ToLowerInvariant();
        var users = unitOfWork.Repository<User>();
        var user = await users.Query()
            .SingleOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);

        var studentCodeOwner = await users.Query()
            .SingleOrDefaultAsync(
                candidate => candidate.StudentCode != null
                    && candidate.StudentCode.ToLower() == studentCode
                    && candidate.Email.ToLower() != email,
                cancellationToken);
        if (studentCodeOwner is not null)
        {
            throw new InvalidOperationException("DefaultStudent:StudentCode is already assigned to another user.");
        }

        if (user is null)
        {
            user = new User
            {
                FullName = defaultStudent.FullName.Trim(),
                Email = email,
                Role = "student",
                IsBlocked = false,
                StudentCode = studentCode
            };

            user.PasswordHash = passwordHasher.HashPassword(user, defaultStudent.Password);
            await users.AddAsync(user, cancellationToken);
        }
        else
        {
            user.FullName = string.IsNullOrWhiteSpace(user.FullName)
                ? defaultStudent.FullName.Trim()
                : user.FullName;
            user.Role = "student";
            user.IsBlocked = false;
            user.StudentCode = studentCode;

            if (defaultStudent.UpdatePasswordOnStartup)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, defaultStudent.Password);
            }

            users.Update(user);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(DefaultStudentOptions defaultStudent)
    {
        if (string.IsNullOrWhiteSpace(defaultStudent.Email))
        {
            throw new InvalidOperationException("DefaultStudent:Email is required when default student seeding is enabled.");
        }

        if (!new EmailAddressAttribute().IsValid(defaultStudent.Email))
        {
            throw new InvalidOperationException("DefaultStudent:Email must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(defaultStudent.Password))
        {
            throw new InvalidOperationException("DefaultStudent:Password is required when default student seeding is enabled.");
        }

        if (string.IsNullOrWhiteSpace(defaultStudent.FullName))
        {
            throw new InvalidOperationException("DefaultStudent:FullName is required when default student seeding is enabled.");
        }

        if (string.IsNullOrWhiteSpace(defaultStudent.StudentCode))
        {
            throw new InvalidOperationException("DefaultStudent:StudentCode is required when default student seeding is enabled.");
        }
    }
}
