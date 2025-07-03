using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IdentityService.Services;

public interface IUserManagementService
{
    Task<PagedResult<UserListDto>> GetUsersAsync(UserSearchRequest request);
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<UserDto?> GetUserByEmailAsync(string email);
    Task<UserDto?> CreateUserAsync(CreateUserRequest request);
    Task<UserDto?> UpdateUserAsync(int userId, UpdateUserRequest request);
    Task<bool> DeleteUserAsync(int userId);
    Task<bool> ChangeUserRoleAsync(int userId, string role);
    Task<bool> ChangeUserStatusAsync(int userId, bool isActive);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<bool> UserExistsAsync(string username, string email, int? excludeUserId = null);
    Task<List<string>> GetAvailableRolesAsync();
    Task<int> GetTotalUsersCountAsync();
    Task<bool> CanDeleteUserAsync(int userId, int currentUserId);
}