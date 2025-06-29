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
        _logger.LogInformation("Login request received for user: {Username} [{RequestId}]", loginDto?.Username ?? "null", requestId);
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var sanitizedHeaders = HttpContext.Request.Headers
                .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && 
                           !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                .Select(h => $"{h.Key}: {string.Join(", ", h.Value.ToArray())}");
            
            _logger.LogDebug("Login request details [{RequestId}] - Method: {Method}, Path: {Path}, Headers: {Headers}, RemoteIP: {RemoteIp}", 
                requestId, HttpContext.Request.Method, HttpContext.Request.Path, 
                string.Join(", ", sanitizedHeaders), HttpContext.Connection.RemoteIpAddress);
        }
        
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

            _logger.LogDebug("Calling auth service for user: {Username} [{RequestId}]", loginDto.Username, requestId);
            var authResponse = await _authService.LoginAsync(loginDto);
            
            if (authResponse == null)
            {
                stopwatch.Stop();
                _logger.LogWarning("Login failed for user: {Username} [{RequestId}] - Duration: {Duration}ms", loginDto.Username, requestId, stopwatch.ElapsedMilliseconds);
                return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResult("Invalid username or password"));
            }

            stopwatch.Stop();
            _logger.LogInformation("Login successful for user: {Username} [{RequestId}] - Duration: {Duration}ms", loginDto.Username, requestId, stopwatch.ElapsedMilliseconds);
            return Ok(ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Login successful"));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Login exception for user: {Username} [{RequestId}] - Duration: {Duration}ms", 
                loginDto.Username, requestId, stopwatch.ElapsedMilliseconds);
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
        _logger.LogInformation("Get current user request [{RequestId}]", requestId);
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var sanitizedHeaders = HttpContext.Request.Headers
                .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && 
                           !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                .Select(h => $"{h.Key}: {string.Join(", ", h.Value.ToArray())}");
            
            _logger.LogDebug("Get current user request details [{RequestId}] - Headers: {Headers}", 
                requestId, string.Join(", ", sanitizedHeaders));
        }
        
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogDebug("User ID from token: {UserId} [{RequestId}]", userIdClaim, requestId);
            
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