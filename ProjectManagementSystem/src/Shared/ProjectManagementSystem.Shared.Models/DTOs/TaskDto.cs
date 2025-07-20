using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Shared.Models.DTOs;

public class TaskDto
{
    public Guid Id { get; set; }
    public int TaskNumber { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public required string Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid ProjectId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? AssignedToUserId { get; set; }
    public Guid? ParentTaskId { get; set; }
    
    // Navigation properties
    public ProjectDto? Project { get; set; }
    public UserDto? AssignedTo { get; set; }
    public TaskDto? ParentTask { get; set; }
    public ICollection<TaskDto> SubTasks { get; set; } = new List<TaskDto>();
    public ICollection<Guid> DependentTaskIds { get; set; } = new List<Guid>();
    public ICollection<Guid> DependsOnTaskIds { get; set; } = new List<Guid>();
    
    // Computed properties
    public decimal ProgressPercentage { get; set; }
    public bool IsParentTask => SubTasks.Any();
    public bool CanEditStatus { get; set; } = true;
}

public class CreateTaskDto
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Title { get; set; }
    
    [StringLength(2000)]
    public string? Description { get; set; }
    
    [Required]
    public required string Status { get; set; }
    
    [Required]
    public required string Priority { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    
    [Required]
    public Guid ProjectId { get; set; }
    
    [Required]
    public int AssignedToUserId { get; set; }
    
    public Guid? ParentTaskId { get; set; }
    public ICollection<Guid> DependsOnTaskIds { get; set; } = new List<Guid>();
}

public class UpdateTaskDto
{
    [StringLength(200, MinimumLength = 1)]
    public string? Title { get; set; }
    
    [StringLength(2000)]
    public string? Description { get; set; }
    
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public int? AssignedToUserId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public ICollection<Guid>? DependsOnTaskIds { get; set; }
}

public class TaskCommentDto
{
    public int Id { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid TaskId { get; set; }
    public int UserId { get; set; }
    public UserDto? User { get; set; }
}

public class CreateTaskCommentDto
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public required string Content { get; set; }
}

public class UpdateTaskCommentDto
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public required string Content { get; set; }
}

public class TaskDependencyDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid DependentOnTaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public TaskDto? Task { get; set; }
    public TaskDto? DependentOnTask { get; set; }
}

public class CreateTaskDependencyDto
{
    [Required]
    public Guid TaskId { get; set; }
    
    [Required]
    public Guid DependentOnTaskId { get; set; }
}