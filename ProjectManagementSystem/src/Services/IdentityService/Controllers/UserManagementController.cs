using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "SystemAdminOnly")]
public class UserManagementController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly IUserDeletionService _userDeletionService;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(
        IUserManagementService userManagementService,
        IUserDeletionService userDeletionService,
        ILogger<UserManagementController> logger)
    {
        _userManagementService = userManagementService;
        _userDeletionService = userDeletionService;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of users with filtering and sorting
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserListDto>>>> GetUsers([FromQuery] UserSearchRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== GET USERS REQUEST {RequestId} ===", requestId);
        _logger.LogInformation("Search parameters - Term: '{SearchTerm}', Role: '{Role}', IsActive: {IsActive}, Page: {Page}, PageSize: {PageSize}", 
            request.SearchTerm, request.Role, request.IsActive, request.PageNumber, request.PageSize);

        try
        {
            var result = await _userManagementService.GetUsersAsync(request);
            
            _logger.LogInformation("Users retrieved successfully - Total: {TotalCount}, Page: {PageNumber}, Items: {ItemCount}", 
                result.TotalCount, result.PageNumber, result.Items.Count);
            
            return Ok(ApiResponse<PagedResult<UserListDto>>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users with request {RequestId}", requestId);
            return StatusCode(500, ApiResponse<PagedResult<UserListDto>>.ErrorResult("An error occurred while retrieving users"));
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
    {
        _logger.LogInformation("=== GET USER BY ID REQUEST === ID: {UserId}", id);

        try
        {
            var user = await _userManagementService.GetUserByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", id);
                return NotFound(ApiResponse<UserDto>.ErrorResult($"User with ID {id} not found"));
            }

            _logger.LogInformation("User retrieved successfully - ID: {UserId}, Username: '{Username}'", 
                user.Id, user.Username);
            
            return Ok(ApiResponse<UserDto>.SuccessResult(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with ID {UserId}", id);
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while retrieving the user"));
        }
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== CREATE USER REQUEST {RequestId} ===", requestId);
        _logger.LogInformation("Creating user - Username: '{Username}', Email: '{Email}', Role: '{Role}'", 
            request.Username, request.Email, request.Role);

        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<UserDto>.ErrorResult("Invalid input", errors));
            }

            var user = await _userManagementService.CreateUserAsync(request);
            if (user == null)
            {
                _logger.LogWarning("User creation failed for username '{Username}' - user already exists or invalid data", 
                    request.Username);
                return BadRequest(ApiResponse<UserDto>.ErrorResult("User creation failed. Username or email may already exist, or role is invalid."));
            }

            _logger.LogInformation("User created successfully - ID: {UserId}, Username: '{Username}'", 
                user.Id, user.Username);
            
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, ApiResponse<UserDto>.SuccessResult(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user with request {RequestId}", requestId);
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while creating the user"));
        }
    }

    /// <summary>
    /// Update user information
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== UPDATE USER REQUEST {RequestId} === ID: {UserId}", requestId, id);

        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<UserDto>.ErrorResult("Invalid input", errors));
            }

            var user = await _userManagementService.UpdateUserAsync(id, request);
            if (user == null)
            {
                _logger.LogWarning("User update failed for ID {UserId} - user not found or invalid data", id);
                return NotFound(ApiResponse<UserDto>.ErrorResult($"User with ID {id} not found or update failed"));
            }

            _logger.LogInformation("User updated successfully - ID: {UserId}, Username: '{Username}'", 
                user.Id, user.Username);
            
            return Ok(ApiResponse<UserDto>.SuccessResult(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with ID {UserId}, request {RequestId}", id, requestId);
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while updating the user"));
        }
    }

    /// <summary>
    /// Delete a user with comprehensive dependency cleanup across all services
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(int id)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== DELETE USER WITH DEPENDENCIES REQUEST {RequestId} === ID: {UserId}", requestId, id);

        try
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            // Use the new comprehensive deletion service
            var result = await _userDeletionService.DeleteUserWithDependenciesAsync(id, currentUserId);
            
            if (!result.Success)
            {
                _logger.LogWarning("User deletion with dependencies failed for ID {UserId} - {Error}", id, result.Message);
                return BadRequest(result);
            }

            _logger.LogInformation("User deleted successfully with all dependencies cleaned up - ID: {UserId}", id);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during comprehensive user deletion with ID {UserId}, request {RequestId}", id, requestId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred during the comprehensive user deletion process"));
        }
    }

    /// <summary>
    /// Change user role
    /// </summary>
    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeUserRole(int id, [FromBody] ChangeRoleRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== CHANGE USER ROLE REQUEST {RequestId} === ID: {UserId}, New Role: '{Role}'", 
            requestId, id, request.Role);

        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<object>.ErrorResult("Invalid input", errors));
            }

            var success = await _userManagementService.ChangeUserRoleAsync(id, request.Role);
            if (!success)
            {
                _logger.LogWarning("Role change failed for user ID {UserId} to role '{Role}'", id, request.Role);
                return BadRequest(ApiResponse<object>.ErrorResult($"Failed to change role. User not found or invalid role: {request.Role}"));
            }

            _logger.LogInformation("User role changed successfully - ID: {UserId}, New Role: '{Role}'", id, request.Role);
            
            return Ok(ApiResponse<object>.SuccessResult(new { message = $"User role changed to {request.Role} successfully" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing role for user ID {UserId}, request {RequestId}", id, requestId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while changing user role"));
        }
    }

    /// <summary>
    /// Change user status (active/inactive)
    /// </summary>
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeUserStatus(int id, [FromBody] ChangeStatusRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== CHANGE USER STATUS REQUEST {RequestId} === ID: {UserId}, Active: {IsActive}", 
            requestId, id, request.IsActive);

        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<object>.ErrorResult("Invalid input", errors));
            }

            var success = await _userManagementService.ChangeUserStatusAsync(id, request.IsActive);
            if (!success)
            {
                _logger.LogWarning("Status change failed for user ID {UserId} to status {IsActive}", id, request.IsActive);
                return NotFound(ApiResponse<object>.ErrorResult($"User with ID {id} not found"));
            }

            _logger.LogInformation("User status changed successfully - ID: {UserId}, Active: {IsActive}", id, request.IsActive);
            
            return Ok(ApiResponse<object>.SuccessResult(new { 
                message = $"User status changed to {(request.IsActive ? "Active" : "Inactive")} successfully" 
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing status for user ID {UserId}, request {RequestId}", id, requestId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while changing user status"));
        }
    }

    /// <summary>
    /// Change user password
    /// </summary>
    [HttpPost("{id:int}/change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== CHANGE PASSWORD REQUEST {RequestId} === ID: {UserId}", requestId, id);

        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<object>.ErrorResult("Invalid input", errors));
            }

            var success = await _userManagementService.ChangePasswordAsync(id, request);
            if (!success)
            {
                _logger.LogWarning("Password change failed for user ID {UserId}", id);
                return NotFound(ApiResponse<object>.ErrorResult($"User with ID {id} not found or password change failed"));
            }

            _logger.LogInformation("Password changed successfully for user ID: {UserId}", id);
            
            return Ok(ApiResponse<object>.SuccessResult(new { 
                message = "Password changed successfully" 
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user ID {UserId}, request {RequestId}", id, requestId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while changing the password"));
        }
    }

    /// <summary>
    /// Get available roles
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetAvailableRoles()
    {
        _logger.LogInformation("=== GET AVAILABLE ROLES REQUEST ===");

        try
        {
            var roles = await _userManagementService.GetAvailableRolesAsync();
            
            _logger.LogInformation("Available roles retrieved successfully - Count: {RoleCount}", roles.Count);
            
            return Ok(ApiResponse<List<string>>.SuccessResult(roles));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available roles");
            return StatusCode(500, ApiResponse<List<string>>.ErrorResult("An error occurred while retrieving available roles"));
        }
    }

    /// <summary>
    /// Get user statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<ApiResponse<object>>> GetUserStatistics()
    {
        _logger.LogInformation("=== GET USER STATISTICS REQUEST ===");

        try
        {
            var totalUsers = await _userManagementService.GetTotalUsersCountAsync();
            
            var statistics = new
            {
                TotalUsers = totalUsers,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("User statistics retrieved successfully - Total Users: {TotalUsers}", totalUsers);
            
            return Ok(ApiResponse<object>.SuccessResult(statistics));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user statistics");
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while retrieving user statistics"));
        }
    }
}