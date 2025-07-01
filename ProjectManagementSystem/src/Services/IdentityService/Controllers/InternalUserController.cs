using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Only requires authentication, not SystemAdmin
public class InternalUserController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<InternalUserController> _logger;

    public InternalUserController(
        IUserManagementService userManagementService, 
        ILogger<InternalUserController> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    /// <summary>
    /// Get user by ID for internal service-to-service calls
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(int id)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== INTERNAL USER GET REQUEST {RequestId} === ID: {UserId}", requestId, id);

        try
        {
            var user = await _userManagementService.GetUserByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for internal request {RequestId}", id, requestId);
                return NotFound(ApiResponse<UserDto>.ErrorResult($"User with ID {id} not found"));
            }

            _logger.LogInformation("Internal user request successful - ID: {UserId}, Username: '{Username}', Request: {RequestId}", 
                user.Id, user.Username, requestId);
            
            return Ok(ApiResponse<UserDto>.SuccessResult(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with ID {UserId} for internal request {RequestId}", id, requestId);
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while retrieving the user"));
        }
    }

    /// <summary>
    /// Get multiple users by IDs for internal service-to-service calls
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetUsersByIds([FromBody] List<int> userIds)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== INTERNAL USERS BATCH REQUEST {RequestId} === Count: {Count}, IDs: [{UserIds}]", 
            requestId, userIds.Count, string.Join(", ", userIds));

        try
        {
            if (!userIds.Any())
            {
                return Ok(ApiResponse<List<UserDto>>.SuccessResult(new List<UserDto>()));
            }

            var users = new List<UserDto>();
            foreach (var userId in userIds)
            {
                var user = await _userManagementService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    users.Add(user);
                }
                else
                {
                    _logger.LogWarning("User with ID {UserId} not found in batch request {RequestId}", userId, requestId);
                }
            }

            _logger.LogInformation("Internal batch request successful - Requested: {RequestedCount}, Found: {FoundCount}, Request: {RequestId}", 
                userIds.Count, users.Count, requestId);
            
            return Ok(ApiResponse<List<UserDto>>.SuccessResult(users));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for internal batch request {RequestId}", requestId);
            return StatusCode(500, ApiResponse<List<UserDto>>.ErrorResult("An error occurred while retrieving the users"));
        }
    }
}