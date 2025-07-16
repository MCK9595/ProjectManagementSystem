using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.TaskService.Data.Entities;

[Table("task_dependencies")]
public class TaskDependency
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("task_id")]
    public Guid TaskId { get; set; }

    [Required]
    [Column("dependent_on_task_id")]
    public Guid DependentOnTaskId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Task Task { get; set; } = null!;
    public Task DependentOnTask { get; set; } = null!;
}