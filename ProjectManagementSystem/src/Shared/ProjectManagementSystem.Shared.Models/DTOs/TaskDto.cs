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
    public ProjectDto? Project { get; set; }
    public UserDto? AssignedTo { get; set; }
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