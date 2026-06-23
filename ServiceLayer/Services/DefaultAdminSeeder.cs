using System.ComponentModel.DataAnnotations;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class DefaultAdminSeeder(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IOptions<DefaultAdminOptions> options) : IDefaultAdminSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var defaultAdmin = options.Value;
        if (!defaultAdmin.Enabled)
        {
            return;
        }

        Validate(defaultAdmin);

        var email = defaultAdmin.Email.Trim().ToLowerInvariant();
        var users = unitOfWork.Repository<User>();
        var user = await users.Query()
            .SingleOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                FullName = defaultAdmin.FullName.Trim(),
                Email = email,
                Role = "admin",
                IsBlocked = false,
                StudentCode = null
            };

            user.PasswordHash = passwordHasher.HashPassword(user, defaultAdmin.Password);
            await users.AddAsync(user, cancellationToken);
        }
        else
        {
            user.FullName = string.IsNullOrWhiteSpace(user.FullName)
                ? defaultAdmin.FullName.Trim()
                : user.FullName;
            user.Role = "admin";
            user.IsBlocked = false;
            user.StudentCode = null;

            if (defaultAdmin.UpdatePasswordOnStartup)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, defaultAdmin.Password);
            }

            users.Update(user);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(DefaultAdminOptions defaultAdmin)
    {
        if (string.IsNullOrWhiteSpace(defaultAdmin.Email))
        {
            throw new InvalidOperationException("DefaultAdmin:Email is required when default admin seeding is enabled.");
        }

        if (!new EmailAddressAttribute().IsValid(defaultAdmin.Email))
        {
            throw new InvalidOperationException("DefaultAdmin:Email must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(defaultAdmin.Password))
        {
            throw new InvalidOperationException("DefaultAdmin:Password is required when default admin seeding is enabled.");
        }

        if (string.IsNullOrWhiteSpace(defaultAdmin.FullName))
        {
            throw new InvalidOperationException("DefaultAdmin:FullName is required when default admin seeding is enabled.");
        }
    }
}
