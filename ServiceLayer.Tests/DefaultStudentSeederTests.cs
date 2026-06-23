using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace ServiceLayer.Tests;

public sealed class DefaultStudentSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesConfiguredStudentWhenEnabled()
    {
        await using var context = CreateContext();
        var seeder = CreateSeeder(context, new DefaultStudentOptions
        {
            Enabled = true,
            Email = "Student@FPT.edu.vn",
            Password = "Student@123",
            FullName = "Default Student",
            StudentCode = "SE000001"
        });

        await seeder.SeedAsync();

        var user = await context.Users.SingleAsync();
        Assert.Equal("student@fpt.edu.vn", user.Email);
        Assert.Equal("student", user.Role);
        Assert.Equal("se000001", user.StudentCode);
        Assert.False(user.IsBlocked);
        Assert.NotEqual("Student@123", user.PasswordHash);
    }

    [Fact]
    public async Task SeedAsync_DoesNothingWhenDisabled()
    {
        await using var context = CreateContext();
        var seeder = CreateSeeder(context, new DefaultStudentOptions
        {
            Enabled = false,
            Email = "student@fpt.edu.vn",
            Password = "Student@123",
            FullName = "Default Student",
            StudentCode = "SE000001"
        });

        await seeder.SeedAsync();

        Assert.Empty(context.Users);
    }

    [Fact]
    public async Task SeedAsync_UpdatesExistingAccountWithoutResettingPasswordByDefault()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Existing Student",
            Email = "student@fpt.edu.vn",
            PasswordHash = "existing-hash",
            Role = "admin",
            IsBlocked = true,
            StudentCode = null
        });
        await context.SaveChangesAsync();
        var seeder = CreateSeeder(context, new DefaultStudentOptions
        {
            Enabled = true,
            Email = "student@fpt.edu.vn",
            Password = "Student@123",
            FullName = "Default Student",
            StudentCode = "SE000001",
            UpdatePasswordOnStartup = false
        });

        await seeder.SeedAsync();

        var user = await context.Users.SingleAsync();
        Assert.Equal("Existing Student", user.FullName);
        Assert.Equal("student", user.Role);
        Assert.Equal("se000001", user.StudentCode);
        Assert.False(user.IsBlocked);
        Assert.Equal("existing-hash", user.PasswordHash);
    }

    private static DefaultStudentSeeder CreateSeeder(AppDbContext context, DefaultStudentOptions options)
    {
        return new DefaultStudentSeeder(
            new UnitOfWork(context),
            new PasswordHasher<User>(),
            Options.Create(options));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
