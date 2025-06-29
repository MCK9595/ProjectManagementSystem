using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Shared.Models.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUserDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public required string Username { get; set; }
    
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
}

public class UpdateUserDto
{
    [StringLength(50, MinimumLength = 1)]
    public string? FirstName { get; set; }
    
    [StringLength(50, MinimumLength = 1)]
    public string? LastName { get; set; }
    
    [EmailAddress]
    public string? Email { get; set; }
}

public class LoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public required string Token { get; set; }
    public required UserDto User { get; set; }
    public DateTime ExpiresAt { get; set; }
}