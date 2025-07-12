using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProjectManagementSystem.Shared.Common.Exceptions;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.Shared.Common.Controllers;

/// <summary>
/// Base controller providing standardized API response handling
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly ILogger<ApiControllerBase> Logger;

    protected ApiControllerBase(ILogger<ApiControllerBase> logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Returns a successful response with data
    /// </summary>
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string? message = null)
    {
        return Ok(ApiResponse<T>.SuccessResult(data, message));
    }

    /// <summary>
    /// Returns a successful response without data
    /// </summary>
    protected ActionResult<ApiResponse> Success(string? message = null)
    {
        return Ok(ApiResponse.SuccessResult(message));
    }

    /// <summary>
    /// Returns a bad request response
    /// </summary>
    protected ActionResult<ApiResponse<T>> BadRequest<T>(string message, List<string>? errors = null)
    {
        return BadRequest(ApiResponse<T>.ErrorResult(message, errors));
    }

    /// <summary>
    /// Returns a bad request response without data
    /// </summary>
    protected new ActionResult<ApiResponse> BadRequest(string message, List<string>? errors = null)
    {
        return base.BadRequest(ApiResponse.ErrorResult(message, errors));
    }

    /// <summary>
    /// Returns an unauthorized response
    /// </summary>
    protected ActionResult<ApiResponse<T>> Unauthorized<T>(string message = "Unauthorized")
    {
        return Unauthorized(ApiResponse<T>.ErrorResult(message));
    }

    /// <summary>
    /// Returns a forbidden response
    /// </summary>
    protected ActionResult<ApiResponse<T>> Forbidden<T>(string message = "Access forbidden")
    {
        return StatusCode(403, ApiResponse<T>.ErrorResult(message));
    }

    /// <summary>
    /// Returns a not found response
    /// </summary>
    protected ActionResult<ApiResponse<T>> NotFound<T>(string message)
    {
        return NotFound(ApiResponse<T>.ErrorResult(message));
    }

    /// <summary>
    /// Returns a conflict response
    /// </summary>
    protected ActionResult<ApiResponse<T>> Conflict<T>(string message)
    {
        return Conflict(ApiResponse<T>.ErrorResult(message));
    }

    /// <summary>
    /// Returns an internal server error response
    /// </summary>
    protected ActionResult<ApiResponse<T>> InternalServerError<T>(string message = "An error occurred while processing your request")
    {
        return StatusCode(500, ApiResponse<T>.ErrorResult(message));
    }

    /// <summary>
    /// Handles exceptions and returns appropriate responses
    /// </summary>
    protected ActionResult<ApiResponse<T>> HandleException<T>(Exception exception, string defaultMessage = "An error occurred")
    {
        return exception switch
        {
            ValidationException validationEx => BadRequest<T>(validationEx.Message, validationEx.Errors),
            NotFoundException notFoundEx => NotFound<T>(notFoundEx.Message),
            UnauthorizedException unauthorizedEx => Unauthorized<T>(unauthorizedEx.Message),
            ForbiddenException forbiddenEx => Forbidden<T>(forbiddenEx.Message),
            ConflictException conflictEx => Conflict<T>(conflictEx.Message),
            AppException appEx => BadRequest<T>(appEx.Message),
            _ => InternalServerError<T>(defaultMessage)
        };
    }

    /// <summary>
    /// Gets the current user ID from claims
    /// </summary>
    protected int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Gets the current username from claims
    /// </summary>
    protected string? GetCurrentUsername()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
    }

    /// <summary>
    /// Gets the current user role from claims
    /// </summary>
    protected string? GetCurrentUserRole()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
    }
}