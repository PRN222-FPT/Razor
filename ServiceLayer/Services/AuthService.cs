using System.Data.Common;
using System.Net.Sockets;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class AuthService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidLoginMessage = "Invalid email or password.";
    private const string ServiceUnavailableMessage = "Authentication service is temporarily unavailable. Please try again later.";

    private static readonly HashSet<string> SupportedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "student",
        "teacher",
        "admin"
    };

    public async Task<LoginResult> ValidateCredentialsAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return LoginResult.Failure(InvalidLoginMessage);
        }

        AuthUserLookupDto? user;
        try
        {
            user = await unitOfWork.Repository<User>()
                .Query()
                .AsNoTracking()
                .Where(candidate => candidate.Email.ToLower() == email)
                .Select(candidate => new AuthUserLookupDto(
                    candidate.UserId,
                    candidate.FullName,
                    candidate.Email,
                    candidate.PasswordHash,
                    candidate.Role,
                    candidate.IsBlocked))
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            logger.LogWarning(ex, "Login validation could not reach the user database.");

            return LoginResult.Failure(ServiceUnavailableMessage);
        }

        if (user is null || user.IsBlocked || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return LoginResult.Failure(InvalidLoginMessage);
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(
            new User
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                Role = user.Role,
                IsBlocked = user.IsBlocked
            },
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return LoginResult.Failure(InvalidLoginMessage);
        }

        var role = NormalizeRole(user.Role);
        if (role is null)
        {
            return LoginResult.Failure("Your account is missing a supported role.");
        }

        return LoginResult.Success(new AuthenticatedUserDto(
            user.UserId,
            user.FullName,
            user.Email,
            role));
    }

    private static string? NormalizeRole(string? role)
    {
        var normalizedRole = role?.Trim().ToLowerInvariant();

        return string.IsNullOrWhiteSpace(normalizedRole) || !SupportedRoles.Contains(normalizedRole)
            ? null
            : normalizedRole;
    }

    private static bool IsDatabaseUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or SocketException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record AuthUserLookupDto(
        Guid UserId,
        string FullName,
        string Email,
        string PasswordHash,
        string? Role,
        bool IsBlocked);
}
