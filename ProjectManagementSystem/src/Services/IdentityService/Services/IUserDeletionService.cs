using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IdentityService.Services;

public interface IUserDeletionService
{
    Task<ApiResponse<object>> DeleteUserWithDependenciesAsync(int userId, int currentUserId);
    Task<ApiResponse<object>> ValidateUserDeletionAsync(int userId, int currentUserId);
    Task<ApiResponse<object>> CleanupUserDependenciesAsync(int userId);
    Task<bool> InvalidateUserSessionsAsync(int userId);
}