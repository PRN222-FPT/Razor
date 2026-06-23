using System.ComponentModel.DataAnnotations;

namespace ServiceLayer.DTOs;

public sealed class DefaultAdminOptions
{
    public const string SectionName = "DefaultAdmin";

    public bool Enabled { get; set; }

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = "System Administrator";

    public bool UpdatePasswordOnStartup { get; set; }
}
