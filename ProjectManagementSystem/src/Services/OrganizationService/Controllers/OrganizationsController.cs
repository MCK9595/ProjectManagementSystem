using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.OrganizationService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly Services.IOrganizationService _organizationService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        Services.IOrganizationService organizationService, 
        ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }

    /// <summary>
    /// Get organizations for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrganizationDto>>>> GetOrganizations(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<PagedResult<OrganizationDto>>.ErrorResult("Invalid token"));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _organizationService.GetOrganizationsAsync(userId.Value, page, pageSize);
            
            return Ok(ApiResponse<PagedResult<OrganizationDto>>.SuccessResult(result, "Organizations retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organizations for user");
            return StatusCode(500, ApiResponse<PagedResult<OrganizationDto>>.ErrorResult("An error occurred while retrieving organizations"));
        }
    }

    /// <summary>
    /// Get organization by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> GetOrganization(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<OrganizationDto>.ErrorResult("Invalid token"));

            var organization = await _organizationService.GetOrganizationByIdAsync(id, userId.Value);
            
            if (organization == null)
                return NotFound(ApiResponse<OrganizationDto>.ErrorResult("Organization not found"));

            return Ok(ApiResponse<OrganizationDto>.SuccessResult(organization, "Organization retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization {OrganizationId}", id);
            return StatusCode(500, ApiResponse<OrganizationDto>.ErrorResult("An error occurred while retrieving the organization"));
        }
    }

    /// <summary>
    /// Create a new organization
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> CreateOrganization([FromBody] CreateOrganizationDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<OrganizationDto>.ErrorResult("Invalid input", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<OrganizationDto>.ErrorResult("Invalid token"));

            var organization = await _organizationService.CreateOrganizationAsync(createDto, userId.Value);
            
            return CreatedAtAction(
                nameof(GetOrganization), 
                new { id = organization.Id }, 
                ApiResponse<OrganizationDto>.SuccessResult(organization, "Organization created successfully"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            // 重複エラーの場合は409 Conflictを返す
            return Conflict(ApiResponse<OrganizationDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization");
            return StatusCode(500, ApiResponse<OrganizationDto>.ErrorResult("An error occurred while creating the organization"));
        }
    }

    /// <summary>
    /// Update an organization
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> UpdateOrganization(Guid id, [FromBody] UpdateOrganizationDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<OrganizationDto>.ErrorResult("Invalid input", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<OrganizationDto>.ErrorResult("Invalid token"));

            var organization = await _organizationService.UpdateOrganizationAsync(id, updateDto, userId.Value);
            
            if (organization == null)
                return NotFound(ApiResponse<OrganizationDto>.ErrorResult("Organization not found or insufficient permissions"));

            return Ok(ApiResponse<OrganizationDto>.SuccessResult(organization, "Organization updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating organization {OrganizationId}", id);
            return StatusCode(500, ApiResponse<OrganizationDto>.ErrorResult("An error occurred while updating the organization"));
        }
    }

    /// <summary>
    /// Delete an organization
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteOrganization(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<object>.ErrorResult("Invalid token"));

            var success = await _organizationService.DeleteOrganizationAsync(id, userId.Value);
            
            if (!success)
                return NotFound(ApiResponse<object>.ErrorResult("Organization not found or insufficient permissions"));

            return Ok(ApiResponse<object>.SuccessResult(new object(), "Organization deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting organization {OrganizationId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while deleting the organization"));
        }
    }

    /// <summary>
    /// Check if user has admin roles that would block deletion
    /// </summary>
    [HttpGet("user/{userId:int}/admin-roles")]
    [AllowAnonymous] // Allow internal service calls
    public async Task<ActionResult<ApiResponse<object>>> CheckUserAdminRoles(int userId)
    {
        try
        {
            _logger.LogInformation("Checking admin roles for user deletion - UserId: {UserId}", userId);

            var hasBlockingRoles = await _organizationService.HasUserBlockingAdminRolesAsync(userId);
            
            if (hasBlockingRoles)
            {
                _logger.LogWarning("User {UserId} has blocking organization admin roles", userId);
                return BadRequest(ApiResponse<object>.ErrorResult("User is the sole administrator of one or more organizations"));
            }

            return Ok(ApiResponse<object>.SuccessResult(new { message = "No blocking admin roles found" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking admin roles for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while checking admin roles"));
        }
    }

    /// <summary>
    /// Clean up all organization dependencies for a user (for deletion process)
    /// </summary>
    [HttpDelete("user/{userId:int}/dependencies")]
    [AllowAnonymous] // Allow internal service calls
    public async Task<ActionResult<ApiResponse<object>>> CleanupUserDependencies(int userId)
    {
        try
        {
            _logger.LogInformation("Cleaning up organization dependencies for user deletion - UserId: {UserId}", userId);

            var success = await _organizationService.CleanupUserDependenciesAsync(userId);
            
            if (!success)
            {
                _logger.LogError("Failed to cleanup organization dependencies for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to cleanup organization dependencies"));
            }

            _logger.LogInformation("Successfully cleaned up organization dependencies for user {UserId}", userId);
            return Ok(ApiResponse<object>.SuccessResult(new { message = "Organization dependencies cleaned up successfully" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up organization dependencies for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while cleaning up organization dependencies"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}