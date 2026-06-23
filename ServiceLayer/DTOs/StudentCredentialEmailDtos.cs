using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.DTOs;

public sealed class StudentCredentialEmailOptions
{
    public const string SectionName = "StudentCredentialEmail";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public bool UseDefaultCredentials { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required, EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "FPT UniRAG";

    [Required]
    public string Subject { get; set; } = "Your FPT UniRAG student account";
}

public sealed record StudentCredentialEmailRequest(
    string FullName,
    string Email,
    string StudentCode,
    string TemporaryPassword);
