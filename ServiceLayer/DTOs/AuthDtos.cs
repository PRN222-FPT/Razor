namespace ServiceLayer.DTOs;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedUserDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role);

public sealed record LoginResult(
    bool Succeeded,
    AuthenticatedUserDto? User = null,
    string? ErrorMessage = null)
{
    public static LoginResult Success(AuthenticatedUserDto user) => new(true, user);

    public static LoginResult Failure(string message) => new(false, null, message);
}
