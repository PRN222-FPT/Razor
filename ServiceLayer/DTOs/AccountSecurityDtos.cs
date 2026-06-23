namespace ServiceLayer.DTOs;

public sealed record ChangePasswordRequest(
    Guid UserId,
    string CurrentPassword,
    string NewPassword);

public sealed record ChangePasswordResult(
    bool Succeeded,
    string? ErrorMessage = null)
{
    public static ChangePasswordResult Success() => new(true);

    public static ChangePasswordResult Failure(string message) => new(false, message);
}

public sealed record PasswordResetRequestResult(
    bool Succeeded,
    string? Email = null,
    string? FullName = null,
    string? ResetToken = null,
    string? ErrorMessage = null)
{
    public static PasswordResetRequestResult Success(
        string email,
        string fullName,
        string resetToken) => new(true, email, fullName, resetToken);

    public static PasswordResetRequestResult Ignored() => new(true);

    public static PasswordResetRequestResult Failure(string message) => new(false, null, null, null, message);
}

public sealed record ResetPasswordRequest(
    string Email,
    string ResetToken,
    string NewPassword);

public sealed record ResetPasswordResult(
    bool Succeeded,
    string? ErrorMessage = null)
{
    public static ResetPasswordResult Success() => new(true);

    public static ResetPasswordResult Failure(string message) => new(false, message);
}

public sealed record PasswordResetEmailRequest(
    string FullName,
    string Email,
    string ResetLink);
