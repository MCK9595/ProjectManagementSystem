using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProjectManagementSystem.Shared.Common.Constants;
using PriorityConstants = ProjectManagementSystem.Shared.Common.Constants.Priority;

namespace ProjectManagementSystem.ProjectService.Data.Entities;

[Table("projects")]
public class Project
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("name")]
    [StringLength(100)]
    public required string Name { get; set; }

    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = ProjectStatus.Planning;

    [Required]
    [Column("priority")]
    [StringLength(50)]
    public string Priority { get; set; } = PriorityConstants.Medium;

    [Column("start_date")]
    public DateTime? StartDate { get; set; }

    [Column("end_date")]
    public DateTime? EndDate { get; set; }

    [Required]
    [Column("organization_id")]
    public Guid OrganizationId { get; set; }

    [Required]
    [Column("created_by_user_id")]
    public int CreatedByUserId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("holiday_dates")]
    public string? HolidayDatesJson { get; set; }

    // Navigation properties
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}