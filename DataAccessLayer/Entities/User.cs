using System;
using System.Collections.Generic;

namespace DataAccessLayer.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Role { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool IsBlocked { get; set; }

    public string? StudentCode { get; set; }

    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public virtual ICollection<BenchmarkRun> BenchmarkRuns { get; set; } = new List<BenchmarkRun>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
