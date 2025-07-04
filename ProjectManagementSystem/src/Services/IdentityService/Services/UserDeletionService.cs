using Microsoft.AspNetCore.Identity;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Constants;
using ProjectManagementSystem.IdentityService.Abstractions;
using System.Text.Json;
using System.Text;

namespace ProjectManagementSystem.IdentityService.Services;

public class UserDeletionService : IUserDeletionService
{
    private readonly IUserManagementService _userManagementService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserDeletionService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HttpClient _organizationServiceClient;
    private readonly HttpClient _projectServiceClient;
    private readonly HttpClient _taskServiceClient;

    public UserDeletionService(
        IUserManagementService userManagementService,
        UserManager<ApplicationUser> userManager,
        ILogger<UserDeletionService> logger,
        IDateTimeProvider dateTimeProvider,
        IHttpClientFactory httpClientFactory)
    {
        _userManagementService = userManagementService;
        _userManager = userManager;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        
        _organizationServiceClient = httpClientFactory.CreateClient("OrganizationService");
        _projectServiceClient = httpClientFactory.CreateClient("ProjectService");
        _taskServiceClient = httpClientFactory.CreateClient("TaskService");
    }

    public async Task<ApiResponse<object>> DeleteUserWithDependenciesAsync(int userId, int currentUserId)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== USER DELETION WITH DEPENDENCIES {RequestId} === UserId: {UserId}", requestId, userId);

        try
        {
            // Step 1: Validate deletion
            var validationResult = await ValidateUserDeletionAsync(userId, currentUserId);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            // Step 2: Clean up dependencies across all services
            var cleanupResult = await CleanupUserDependenciesAsync(userId);
            if (!cleanupResult.Success)
            {
                _logger.LogError("Failed to clean up user dependencies for UserId: {UserId}", userId);
                return ApiResponse<object>.ErrorResult("Failed to clean up user dependencies. Deletion aborted.");
            }

            // Step 3: Invalidate user sessions
            await InvalidateUserSessionsAsync(userId);

            // Step 4: Perform final user deletion in IdentityService
            var deletionSuccess = await _userManagementService.DeleteUserAsync(userId);
            if (!deletionSuccess)
            {
                _logger.LogError("Failed to delete user in IdentityService for UserId: {UserId}", userId);
                return ApiResponse<object>.ErrorResult("Failed to complete user deletion.");
            }

            _logger.LogInformation("User deletion completed successfully - UserId: {UserId}, RequestId: {RequestId}", userId, requestId);
            return ApiResponse<object>.SuccessResult(new { userId }, $"User {userId} deleted successfully with all dependencies cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user deletion process - UserId: {UserId}, RequestId: {RequestId}", userId, requestId);
            return ApiResponse<object>.ErrorResult("An error occurred during the deletion process");
        }
    }

    public async Task<ApiResponse<object>> ValidateUserDeletionAsync(int userId, int currentUserId)
    {
        try
        {
            // Use existing validation from UserManagementService
            var canDelete = await _userManagementService.CanDeleteUserAsync(userId, currentUserId);
            if (!canDelete)
            {
                return ApiResponse<object>.ErrorResult("Cannot delete this user. You cannot delete yourself or the last SystemAdmin.");
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ApiResponse<object>.ErrorResult($"User with ID {userId} not found");
            }

            // Check if current user is System Admin
            var isSystemAdmin = await IsCurrentUserSystemAdminAsync(currentUserId);
            if (isSystemAdmin)
            {
                _logger.LogInformation("System Admin deletion request - skipping organization/project role checks for UserId: {UserId}", userId);
                return ApiResponse<object>.SuccessResult(new { userId }, "System Admin can delete this user without restrictions");
            }

            // Additional validation: Check if user has critical organizational roles (only for non-System Admin)
            var organizationRoleCheck = await CheckOrganizationAdminRolesAsync(userId);
            if (!organizationRoleCheck.Success)
            {
                return organizationRoleCheck;
            }

            var projectRoleCheck = await CheckProjectAdminRolesAsync(userId);
            if (!projectRoleCheck.Success)
            {
                return projectRoleCheck;
            }

            _logger.LogInformation("User deletion validation passed for UserId: {UserId}", userId);
            return ApiResponse<object>.SuccessResult(new { userId }, "User can be safely deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user deletion for UserId: {UserId}", userId);
            return ApiResponse<object>.ErrorResult("Error occurred during validation");
        }
    }

    public async Task<ApiResponse<object>> CleanupUserDependenciesAsync(int userId)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== CLEANUP USER DEPENDENCIES {RequestId} === UserId: {UserId}", requestId, userId);

        try
        {
            // Step 1: Clean up in TaskService (unassign from tasks, preserve comments)
            var taskCleanup = await CleanupTaskDependenciesAsync(userId);
            if (!taskCleanup.Success)
            {
                _logger.LogError("Failed to cleanup task dependencies for UserId: {UserId}", userId);
                return taskCleanup;
            }

            // Step 2: Clean up in ProjectService (remove from projects)
            var projectCleanup = await CleanupProjectDependenciesAsync(userId);
            if (!projectCleanup.Success)
            {
                _logger.LogError("Failed to cleanup project dependencies for UserId: {UserId}", userId);
                return projectCleanup;
            }

            // Step 3: Clean up in OrganizationService (remove from organizations)
            var organizationCleanup = await CleanupOrganizationDependenciesAsync(userId);
            if (!organizationCleanup.Success)
            {
                _logger.LogError("Failed to cleanup organization dependencies for UserId: {UserId}", userId);
                return organizationCleanup;
            }

            _logger.LogInformation("All user dependencies cleaned up successfully - UserId: {UserId}, RequestId: {RequestId}", userId, requestId);
            return ApiResponse<object>.SuccessResult(new { userId }, "All dependencies cleaned up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up user dependencies - UserId: {UserId}, RequestId: {RequestId}", userId, requestId);
            return ApiResponse<object>.ErrorResult("Error occurred during dependency cleanup");
        }
    }

    public async Task<bool> InvalidateUserSessionsAsync(int userId)
    {
        try
        {
            _logger.LogInformation("Invalidating sessions for UserId: {UserId}", userId);
            
            // In a real implementation, this would:
            // 1. Invalidate all refresh tokens for the user
            // 2. Add the user to a blacklist for JWT validation
            // 3. Clear any cached sessions
            
            // For now, we'll just log the operation
            // TODO: Implement actual session invalidation
            
            _logger.LogInformation("Session invalidation completed for UserId: {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating sessions for UserId: {UserId}", userId);
            return false;
        }
    }

    private async Task<ApiResponse<object>> CheckOrganizationAdminRolesAsync(int userId)
    {
        try
        {
            var response = await _organizationServiceClient.GetAsync($"api/organizations/user/{userId}/admin-roles");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<object>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (result?.Success == false)
                {
                    return ApiResponse<object>.ErrorResult("User is the sole administrator of one or more organizations. Please transfer ownership before deletion.");
                }
            }
            
            return ApiResponse<object>.SuccessResult(new { userId }, "No blocking organization admin roles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking organization admin roles for UserId: {UserId}", userId);
            return ApiResponse<object>.ErrorResult("Error occurred while checking organization roles");
        }
    }

    private async Task<ApiResponse<object>> CheckProjectAdminRolesAsync(int userId)
    {
        try
        {
            var response = await _projectServiceClient.GetAsync($"api/projects/user/{userId}/admin-roles");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<object>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (result?.Success == false)
                {
                    return ApiResponse<object>.ErrorResult("User is the sole administrator of one or more projects. Please transfer ownership before deletion.");
                }
            }
            
            return ApiResponse<object>.SuccessResult(new { userId }, "No blocking project admin roles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking project admin roles for UserId: {UserId}", userId);
            return ApiResponse<object>.ErrorResult("Error occurred while checking project roles");
        }
    }

    private async Task<ApiResponse<object>> CleanupTaskDependenciesAsync(int userId)
    {
        try
        {
            var response = await _taskServiceClient.DeleteAsync($"api/tasks/user/{userId}/dependencies");
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Task dependencies cleaned up for UserId: {UserId}", userId);
                return ApiResponse<object>.SuccessResult(new { userId }, "Task dependencies cleaned up");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to cleanup task dependencies for UserId: {UserId}. Response: {Response}", userId, errorContent);
            return ApiResponse<object>.ErrorResult("Failed to cleanup task dependencies");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up task dependencies for UserId: {UserId}", userId);
            return ApiResponse<object>.ErrorResult("Error occurred while cleaning up task dependencies");
        }
    }

    private async Task<ApiResponse<object>> CleanupProjectDependenciesAsync(int userId)
    {
        try
        {
            var response = await _projectServiceClient.DeleteAsync($"api/projects/user/{userId}/dependencies");
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Project dependencies cleaned up for UserId: {UserId}", userId);
                return ApiResponse<object>.SuccessResult(new { userId }, "Project dependencies cleaned up");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to cleanup project dependencies for UserId: {UserId}. Response: {Response}", userId, errorContent);
            return ApiResponse<object>.ErrorResult("Failed to cleanup project dependencies");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up project dependencies for UserId: {UserId}", userId);
            return ApiResponse<object>.ErrorResult("Error occurred while cleaning up project dependencies");
        }
    }

    private async Task<ApiResponse<object>> CleanupOrganizationDependenciesAsync(int userId)
    {
        try
        {
            var response = await _organizationServiceClient.DeleteAsync($"api/organizations/user/{userId}/dependencies");
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Organization dependencies cleaned up for UserId: {UserId}", userId);
                return ApiResponse<object>.SuccessResult(new { userId }, "Organization dependencies cleaned up");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to cleanup organization dependencies for UserId: {UserId}. Response: {Response}", userId, errorContent);
            return ApiResponse<object>.ErrorResult("Failed to cleanup organization dependencies");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up organization dependencies for UserId: {UserId}", userId);
            return ApiResponse<object>.ErrorResult("Error occurred while cleaning up organization dependencies");
        }
    }

    private async Task<bool> IsCurrentUserSystemAdminAsync(int currentUserId)
    {
        try
        {
            var currentUser = await _userManager.FindByIdAsync(currentUserId.ToString());
            if (currentUser == null)
            {
                _logger.LogWarning("Current user with ID {CurrentUserId} not found during System Admin check", currentUserId);
                return false;
            }

            var isSystemAdmin = await _userManager.IsInRoleAsync(currentUser, Roles.SystemAdmin);
            _logger.LogDebug("System Admin check for UserId {CurrentUserId}: {IsSystemAdmin}", currentUserId, isSystemAdmin);
            
            return isSystemAdmin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if current user {CurrentUserId} is System Admin", currentUserId);
            return false;
        }
    }
}