using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class AccountSecurityService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher) : IAccountSecurityService
{
    private const int MinimumPasswordLength = 8;
    private const int ResetTokenByteLength = 32;
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    private static readonly HashSet<string> ManagedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "student",
        "teacher",
        "admin"
    };

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsPasswordAcceptable(request.NewPassword))
        {
            return ChangePasswordResult.Failure($"New password must be at least {MinimumPasswordLength} characters long.");
        }

        var user = await unitOfWork.Repository<User>()
            .Query()
            .SingleOrDefaultAsync(candidate => candidate.UserId == request.UserId, cancellationToken);
        if (user is null)
        {
            return ChangePasswordResult.Failure("Your account was not found.");
        }

        if (!CanUseAccount(user) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return ChangePasswordResult.Failure("Your account is not eligible for password changes.");
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return ChangePasswordResult.Failure("Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        ClearPasswordResetState(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ChangePasswordResult.Success();
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return PasswordResetRequestResult.Failure("Email is required.");
        }

        var user = await unitOfWork.Repository<User>()
            .Query()
            .SingleOrDefaultAsync(candidate => candidate.Email.ToLower() == normalizedEmail, cancellationToken);
        if (user is null || !CanUseAccount(user) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return PasswordResetRequestResult.Ignored();
        }

        var token = GenerateResetToken();
        user.PasswordResetTokenHash = ComputeTokenHash(token);
        user.PasswordResetTokenExpiresAt = CurrentTimestamp().Add(ResetTokenLifetime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return PasswordResetRequestResult.Success(user.Email, user.FullName, token);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsPasswordAcceptable(request.NewPassword))
        {
            return ResetPasswordResult.Failure($"New password must be at least {MinimumPasswordLength} characters long.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        if (normalizedEmail is null || string.IsNullOrWhiteSpace(request.ResetToken))
        {
            return ResetPasswordResult.Failure("The reset link is invalid or expired.");
        }

        var user = await unitOfWork.Repository<User>()
            .Query()
            .SingleOrDefaultAsync(candidate => candidate.Email.ToLower() == normalizedEmail, cancellationToken);
        if (user is null || !CanUseAccount(user) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return ResetPasswordResult.Failure("The reset link is invalid or expired.");
        }

        if (user.PasswordResetTokenHash is null || user.PasswordResetTokenExpiresAt is null)
        {
            return ResetPasswordResult.Failure("The reset link is invalid or expired.");
        }

        if (user.PasswordResetTokenExpiresAt <= CurrentTimestamp())
        {
            return ResetPasswordResult.Failure("The reset link is invalid or expired.");
        }

        var suppliedTokenHash = ComputeTokenHash(request.ResetToken);
        if (!string.Equals(user.PasswordResetTokenHash, suppliedTokenHash, StringComparison.Ordinal))
        {
            return ResetPasswordResult.Failure("The reset link is invalid or expired.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        ClearPasswordResetState(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ResetPasswordResult.Success();
    }

    public async Task ClearPasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return;
        }

        var user = await unitOfWork.Repository<User>()
            .Query()
            .SingleOrDefaultAsync(candidate => candidate.Email.ToLower() == normalizedEmail, cancellationToken);
        if (user is null)
        {
            return;
        }

        ClearPasswordResetState(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool CanUseAccount(User user)
    {
        return !string.IsNullOrWhiteSpace(user.Role)
            && ManagedRoles.Contains(user.Role.Trim());
    }

    private static bool IsPasswordAcceptable(string password)
    {
        return !string.IsNullOrWhiteSpace(password) && password.Trim().Length >= MinimumPasswordLength;
    }

    private static string? NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) || !EmailAddressValidator.IsValid(normalized)
            ? null
            : normalized;
    }

    private static string GenerateResetToken()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(ResetTokenByteLength));
    }

    private static string ComputeTokenHash(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static DateTime CurrentTimestamp()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    private static void ClearPasswordResetState(User user)
    {
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
    }
}
