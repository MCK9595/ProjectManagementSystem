using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.IdentityService.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
    Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);
    Task<UserDto?> GetCurrentUserAsync(int userId);
    Task<ApplicationUser?> GetUserByIdAsync(int userId);
    Task<ApplicationUser?> GetUserByUsernameAsync(string username);
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
}