using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Shared.Models.DTOs;

public class ProjectDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public required string Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid OrganizationId { get; set; }
    public int CreatedByUserId { get; set; }
    public List<DateTime> HolidayDates { get; set; } = new();
    public OrganizationDto? Organization { get; set; }
}

public class CreateProjectDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; set; }
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    public required string Status { get; set; }
    
    [Required]
    public required string Priority { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    public List<DateTime>? HolidayDates { get; set; }
    
    [Required]
    public Guid OrganizationId { get; set; }
}

public class UpdateProjectDto
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<DateTime>? HolidayDates { get; set; }
}

public class ProjectMemberDto
{
    public int Id { get; set; }
    public Guid ProjectId { get; set; }
    public int UserId { get; set; }
    public required string Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public UserDto? User { get; set; }
}

public class AddProjectMemberDto
{
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public required string Role { get; set; }
}