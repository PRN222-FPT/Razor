namespace ServiceLayer.DTOs;

public sealed record TeacherCredentialEmailRequest(
    string FullName,
    string Email,
    string TemporaryPassword);
