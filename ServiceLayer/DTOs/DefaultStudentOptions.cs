using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.DTOs;

public sealed class DefaultStudentOptions
{
    public const string SectionName = "DefaultStudent";

    public bool Enabled { get; set; }

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = "Default Student";

    public string StudentCode { get; set; } = "se000000";

    public bool UpdatePasswordOnStartup { get; set; }
}
