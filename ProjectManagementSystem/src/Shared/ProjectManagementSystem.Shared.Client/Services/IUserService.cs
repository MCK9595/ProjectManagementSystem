using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Models.Common;

namespace ProjectManagementSystem.Shared.Client.Services;

public interface IUserService
{
    Task<PagedResult<UserListDto>?> GetUsersAsync(UserSearchRequest request);
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<UserDto?> CreateUserAsync(CreateUserRequest request);
    Task<UserDto?> UpdateUserAsync(int userId, UpdateUserRequest request);
    Task<bool> DeleteUserAsync(int userId);
    Task<bool> ChangeUserRoleAsync(int userId, string role);
    Task<bool> ChangeUserStatusAsync(int userId, bool isActive);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<List<string>?> GetAvailableRolesAsync();
    Task<object?> GetUserStatisticsAsync();
    string? GetLastError();
}