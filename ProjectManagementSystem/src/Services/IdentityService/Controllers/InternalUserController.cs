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

    /// <summary>
    /// Find user by email or create new user for internal service-to-service calls
    /// </summary>
    [HttpPost("find-or-create")]
    public async Task<ActionResult<ApiResponse<UserDto>>> FindOrCreateUser([FromBody] FindOrCreateUserRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== INTERNAL FIND OR CREATE USER REQUEST {RequestId} === Email: {Email}", requestId, request.Email);

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

            // First, try to find existing user by email
            var existingUser = await _userManagementService.GetUserByEmailAsync(request.Email);
            if (existingUser != null)
            {
                _logger.LogInformation("Found existing user - ID: {UserId}, Email: {Email}, Request: {RequestId}", 
                    existingUser.Id, existingUser.Email, requestId);
                
                return Ok(ApiResponse<UserDto>.SuccessResult(existingUser));
            }

            // User doesn't exist, create new user
            _logger.LogInformation("User not found, creating new user - Email: {Email}, Request: {RequestId}", request.Email, requestId);
            
            var createUserRequest = new CreateUserRequest
            {
                Username = request.Email.Split('@')[0], // Use part before @ as username
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Password = GenerateTemporaryPassword(),
                Role = "User", // Default role for new users created via this endpoint
                IsActive = true
            };

            var newUser = await _userManagementService.CreateUserAsync(createUserRequest);
            if (newUser == null)
            {
                _logger.LogWarning("User creation failed for email '{Email}' - user may already exist or invalid data, Request: {RequestId}", 
                    request.Email, requestId);
                return BadRequest(ApiResponse<UserDto>.ErrorResult("User creation failed. Email may already exist or data is invalid."));
            }

            _logger.LogInformation("User created successfully - ID: {UserId}, Email: {Email}, Request: {RequestId}", 
                newUser.Id, newUser.Email, requestId);
            
            return Ok(ApiResponse<UserDto>.SuccessResult(newUser));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in find or create user request {RequestId}", requestId);
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while processing the user request"));
        }
    }

    /// <summary>
    /// Create a new user with specified password for internal service-to-service calls
    /// </summary>
    [HttpPost("create-with-password")]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUserWithPassword([FromBody] CreateUserWithPasswordRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== INTERNAL CREATE USER WITH PASSWORD REQUEST {RequestId} === Email: {Email}", requestId, request.Email);

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

            // Validate password confirmation
            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest(ApiResponse<UserDto>.ErrorResult("Password and confirm password do not match"));
            }

            var createUserRequest = new CreateUserRequest
            {
                Username = request.Email.Split('@')[0], // Use part before @ as username
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Password = request.Password,
                Role = "User", // Default role for new users created via this endpoint
                IsActive = true
            };

            var newUser = await _userManagementService.CreateUserAsync(createUserRequest);
            if (newUser == null)
            {
                _logger.LogWarning("User creation failed for email '{Email}' - user may already exist or invalid data, Request: {RequestId}", 
                    request.Email, requestId);
                return BadRequest(ApiResponse<UserDto>.ErrorResult("User creation failed. Email may already exist or data is invalid."));
            }

            _logger.LogInformation("User created successfully with password - ID: {UserId}, Email: {Email}, Request: {RequestId}", 
                newUser.Id, newUser.Email, requestId);
            
            return Ok(ApiResponse<UserDto>.SuccessResult(newUser));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in create user with password request {RequestId}", requestId);
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while creating the user"));
        }
    }

    /// <summary>
    /// Check if user exists by email for internal service-to-service calls
    /// </summary>
    [HttpGet("check-email/{email}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> CheckUserExistsByEmail(string email)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== INTERNAL CHECK USER EMAIL REQUEST {RequestId} === Email: {Email}", requestId, email);

        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(ApiResponse<UserDto>.ErrorResult("Email is required"));
            }

            var user = await _userManagementService.GetUserByEmailAsync(email);
            if (user != null)
            {
                _logger.LogInformation("User found for email check - ID: {UserId}, Email: {Email}, Request: {RequestId}", 
                    user.Id, user.Email, requestId);
                
                return Ok(ApiResponse<UserDto>.SuccessResult(user));
            }
            else
            {
                _logger.LogInformation("User not found for email check - Email: {Email}, Request: {RequestId}", email, requestId);
                return NotFound(ApiResponse<UserDto>.ErrorResult("User not found"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user email {Email} for request {RequestId}", email, requestId);
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while checking the user email"));
        }
    }

    private string GenerateTemporaryPassword()
    {
        // Generate a secure temporary password
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}