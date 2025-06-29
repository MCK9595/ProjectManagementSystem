using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.IdentityService.Data.Entities;

[Table("AspNetUsers")]
public class ApplicationUser : IdentityUser<int>
{
    [Required]
    [Column("first_name")]
    [StringLength(50)]
    public required string FirstName { get; set; }

    [Required]
    [Column("last_name")]
    [StringLength(50)]
    public required string LastName { get; set; }

    [Column("role")]
    [StringLength(50)]
    public string Role { get; set; } = Roles.OrganizationMember;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("failed_login_attempts")]
    public int FailedLoginAttempts { get; set; } = 0;

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;
}