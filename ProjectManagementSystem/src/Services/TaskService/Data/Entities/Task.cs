using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProjectManagementSystem.Shared.Common.Constants;
using PriorityConstants = ProjectManagementSystem.Shared.Common.Constants.Priority;
using TaskStatusConstants = ProjectManagementSystem.Shared.Common.Constants.TaskStatus;

namespace ProjectManagementSystem.TaskService.Data.Entities;

[Table("tasks")]
public class Task
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("task_number")]
    public int TaskNumber { get; set; }

    [Required]
    [Column("title")]
    [StringLength(200)]
    public required string Title { get; set; }

    [Column("description")]
    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = TaskStatusConstants.ToDo;

    [Required]
    [Column("priority")]
    [StringLength(50)]
    public string Priority { get; set; } = PriorityConstants.Medium;

    [Column("start_date")]
    public DateTime? StartDate { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("completed_date")]
    public DateTime? CompletedDate { get; set; }

    [Required]
    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Required]
    [Column("assigned_to_user_id")]
    public int AssignedToUserId { get; set; }

    [Required]
    [Column("created_by_user_id")]
    public int CreatedByUserId { get; set; }

    [Column("estimated_hours")]
    public decimal? EstimatedHours { get; set; }

    [Column("actual_hours")]
    public decimal? ActualHours { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}