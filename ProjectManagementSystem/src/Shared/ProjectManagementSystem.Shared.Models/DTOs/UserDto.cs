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

// User Management DTOs
public class UserListDto
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string FullName => $"{FirstName} {LastName}";
}

public class CreateUserRequest
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
    
    [Required]
    public required string Role { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class UpdateUserRequest
{
    [StringLength(50, MinimumLength = 1)]
    public string? FirstName { get; set; }
    
    [StringLength(50, MinimumLength = 1)]
    public string? LastName { get; set; }
    
    [EmailAddress]
    public string? Email { get; set; }
    
    public string? Role { get; set; }
    
    public bool? IsActive { get; set; }
}

public class ChangeRoleRequest
{
    [Required]
    public required string Role { get; set; }
}

public class ChangeStatusRequest
{
    [Required]
    public required bool IsActive { get; set; }
}

public class UserSearchRequest : ProjectManagementSystem.Shared.Models.Common.PagingParameters
{
    public new string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string NewPassword { get; set; }
}

public class FindOrCreateUserRequest
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
}

public class CreateUserWithPasswordRequest
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
}