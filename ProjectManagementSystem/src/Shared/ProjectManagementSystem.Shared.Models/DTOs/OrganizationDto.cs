using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Shared.Models.DTOs;

public class OrganizationDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public int CreatedByUserId { get; set; }
}

public class CreateOrganizationDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
}

public class UpdateOrganizationDto
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
}

public class OrganizationMemberDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int UserId { get; set; }
    public required string Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public UserDto? User { get; set; }
}

public class AddMemberDto
{
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public required string Role { get; set; }
}