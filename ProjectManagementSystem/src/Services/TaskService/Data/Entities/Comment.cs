using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.TaskService.Data.Entities;

[Table("comments")]
public class Comment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("content")]
    [StringLength(2000)]
    public required string Content { get; set; }

    [Required]
    [Column("task_id")]
    public Guid TaskId { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(TaskId))]
    public Task Task { get; set; } = null!;
}