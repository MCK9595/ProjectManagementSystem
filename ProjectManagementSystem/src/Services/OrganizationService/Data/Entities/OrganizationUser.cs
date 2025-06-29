using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.OrganizationService.Data.Entities;

[Table("organization_users")]
public class OrganizationUser
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("organization_id")]
    public int OrganizationId { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [Column("role")]
    [StringLength(50)]
    public string Role { get; set; } = Roles.OrganizationMember;

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [ForeignKey(nameof(OrganizationId))]
    public Organization Organization { get; set; } = null!;
}