using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user and return JWT token
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto loginDto)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== LOGIN REQUEST {RequestId} RECEIVED ===", requestId);
        _logger.LogInformation("Request received for user: {Username}", loginDto?.Username ?? "null");
        _logger.LogInformation("Request Method: {Method}, Path: {Path}", HttpContext.Request.Method, HttpContext.Request.Path);
        _logger.LogInformation("Request Headers: {Headers}", 
            string.Join(", ", HttpContext.Request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value.ToArray())}")));
        _logger.LogInformation("Remote IP: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
        _logger.LogInformation("Content-Type: {ContentType}", HttpContext.Request.ContentType);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResult("Invalid input", errors));
            }

            _logger.LogInformation("Calling auth service for user: {Username}", loginDto.Username);
            var authResponse = await _authService.LoginAsync(loginDto);
            
            if (authResponse == null)
            {
                stopwatch.Stop();
                _logger.LogWarning("=== LOGIN FAILED {RequestId} === Duration: {Duration}ms", requestId, stopwatch.ElapsedMilliseconds);
                return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResult("Invalid username or password"));
            }

            stopwatch.Stop();
            _logger.LogInformation("=== LOGIN SUCCESSFUL {RequestId} === Duration: {Duration}ms", requestId, stopwatch.ElapsedMilliseconds);
            return Ok(ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Login successful"));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "=== LOGIN EXCEPTION {RequestId} === Error during login for user {Username}, Duration: {Duration}ms", 
                requestId, loginDto.Username, stopwatch.ElapsedMilliseconds);
            return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResult("An error occurred during login"));
        }
    }

    /// <summary>
    /// Refresh JWT token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResult("Refresh token is required"));
            }

            var authResponse = await _authService.RefreshTokenAsync(request.RefreshToken);
            
            if (authResponse == null)
            {
                return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResult("Invalid or expired refresh token"));
            }

            return Ok(ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Token refreshed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResult("An error occurred during token refresh"));
        }
    }

    /// <summary>
    /// Logout user and revoke refresh token
    /// </summary>
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public async Task<ActionResult<ApiResponse<object>>> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Refresh token is required"));
            }

            var success = await _authService.LogoutAsync(request.RefreshToken);
            
            if (success)
            {
                return Ok(ApiResponse<object>.SuccessResult(new object(), "Logout successful"));
            }
            else
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Invalid refresh token"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred during logout"));
        }
    }

    /// <summary>
    /// Get current authenticated user information
    /// </summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetCurrentUser()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("=== GET CURRENT USER REQUEST {RequestId} ===", requestId);
        _logger.LogInformation("Request Headers: {Headers}", 
            string.Join(", ", HttpContext.Request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value.ToArray())}")));
        
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("User ID from token: {UserId}", userIdClaim);
            
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<UserDto>.ErrorResult("Invalid token"));
            }

            var user = await _authService.GetCurrentUserAsync(userId);
            
            if (user == null)
            {
                return NotFound(ApiResponse<UserDto>.ErrorResult("User not found"));
            }

            return Ok(ApiResponse<UserDto>.SuccessResult(user, "User information retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(500, ApiResponse<UserDto>.ErrorResult("An error occurred while retrieving user information"));
        }
    }

    /// <summary>
    /// Validate JWT token (for other services to verify tokens)
    /// </summary>
    [HttpPost("validate")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public ActionResult<ApiResponse<object>> ValidateToken()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userIdClaim == null || usernameClaim == null)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Invalid token"));
            }

            var tokenInfo = new
            {
                UserId = userIdClaim,
                Username = usernameClaim,
                Role = roleClaim,
                IsValid = true
            };

            return Ok(ApiResponse<object>.SuccessResult(tokenInfo, "Token is valid"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while validating token"));
        }
    }
}

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}