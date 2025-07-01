using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.ProjectService.Data.Entities;

[Table("project_members")]
public class ProjectMember
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [Column("role")]
    [StringLength(50)]
    public string Role { get; set; } = Roles.ProjectMember;

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;
}