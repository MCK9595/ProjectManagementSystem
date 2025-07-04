using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Shared.Models.DTOs;

public class OrganizationDto
{
    public Guid Id { get; set; }
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
    public Guid OrganizationId { get; set; }
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

public class AddMemberByEmailDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string FirstName { get; set; }
    
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string LastName { get; set; }
    
    [Required]
    public required string OrganizationRole { get; set; }
}

public class FindUserByEmailDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    
    [Required]
    public required string OrganizationRole { get; set; }
}

public class CreateUserAndAddMemberDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string FirstName { get; set; }
    
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string LastName { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string Password { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string ConfirmPassword { get; set; }
    
    [Required]
    public required string OrganizationRole { get; set; }
}