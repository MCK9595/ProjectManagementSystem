using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Shared.Common.Constants;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Security.Claims;

namespace ProjectManagementSystem.ProjectService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly Services.IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(Services.IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet("organization/{organizationId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectDto>>>> GetProjects(
        Guid organizationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var projects = await _projectService.GetProjectsAsync(organizationId, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResult<ProjectDto>>.SuccessResult(projects));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects for organization {OrganizationId}", organizationId);
            return StatusCode(500, ApiResponse<PagedResult<ProjectDto>>.ErrorResult("Internal server error"));
        }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectDto>>>> GetUserProjects(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<PagedResult<ProjectDto>>.ErrorResult("Invalid token"));

            var projects = await _projectService.GetProjectsByUserAsync(userId.Value, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResult<ProjectDto>>.SuccessResult(projects));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects for user");
            return StatusCode(500, ApiResponse<PagedResult<ProjectDto>>.ErrorResult("Internal server error"));
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<ProjectDto>.ErrorResult("Invalid token"));
            
            if (!await _projectService.HasProjectAccessAsync(id, userId.Value))
            {
                return Forbid();
            }

            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null)
            {
                return NotFound(ApiResponse<ProjectDto>.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse<ProjectDto>.SuccessResult(project));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project {ProjectId}", id);
            return StatusCode(500, ApiResponse<ProjectDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> CreateProject([FromBody] CreateProjectDto createProjectDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProjectDto>.ErrorResult("Invalid project data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<ProjectDto>.ErrorResult("Invalid token"));

            var project = await _projectService.CreateProjectAsync(createProjectDto, userId.Value);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, ApiResponse<ProjectDto>.SuccessResult(project));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return StatusCode(500, ApiResponse<ProjectDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> UpdateProject(Guid id, [FromBody] UpdateProjectDto updateProjectDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProjectDto>.ErrorResult("Invalid project data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<ProjectDto>.ErrorResult("Invalid token"));
            
            if (!await _projectService.IsProjectManagerAsync(id, userId.Value) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var project = await _projectService.UpdateProjectAsync(id, updateProjectDto);
            if (project == null)
            {
                return NotFound(ApiResponse<ProjectDto>.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse<ProjectDto>.SuccessResult(project));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", id);
            return StatusCode(500, ApiResponse<ProjectDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse>> DeleteProject(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.ErrorResult("Invalid token"));
            
            if (!await _projectService.IsProjectManagerAsync(id, userId.Value) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _projectService.DeleteProjectAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse.SuccessResult("Project deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", id);
            return StatusCode(500, ApiResponse.ErrorResult("Internal server error"));
        }
    }

    [HttpPost("{id}/archive")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse>> ArchiveProject(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.ErrorResult("Invalid token"));
            
            if (!await _projectService.IsProjectManagerAsync(id, userId.Value) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _projectService.ArchiveProjectAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse.SuccessResult("Project archived successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving project {ProjectId}", id);
            return StatusCode(500, ApiResponse.ErrorResult("Internal server error"));
        }
    }

    [HttpPost("{id}/restore")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse>> RestoreProject(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.ErrorResult("Invalid token"));
            
            if (!await _projectService.IsProjectManagerAsync(id, userId.Value) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _projectService.RestoreProjectAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse.SuccessResult("Project restored successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring project {ProjectId}", id);
            return StatusCode(500, ApiResponse.ErrorResult("Internal server error"));
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
            _logger.LogInformation("Checking project admin roles for user deletion - UserId: {UserId}", userId);

            var hasBlockingRoles = await _projectService.HasUserBlockingAdminRolesAsync(userId);
            
            if (hasBlockingRoles)
            {
                _logger.LogWarning("User {UserId} has blocking project admin roles", userId);
                return BadRequest(ApiResponse<object>.ErrorResult("User is the sole administrator of one or more projects"));
            }

            return Ok(ApiResponse<object>.SuccessResult(new { message = "No blocking admin roles found" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking project admin roles for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while checking project admin roles"));
        }
    }

    /// <summary>
    /// Clean up all project dependencies for a user (for deletion process)
    /// </summary>
    [HttpDelete("user/{userId:int}/dependencies")]
    [AllowAnonymous] // Allow internal service calls
    public async Task<ActionResult<ApiResponse<object>>> CleanupUserDependencies(int userId)
    {
        try
        {
            _logger.LogInformation("Cleaning up project dependencies for user deletion - UserId: {UserId}", userId);

            var success = await _projectService.CleanupUserDependenciesAsync(userId);
            
            if (!success)
            {
                _logger.LogError("Failed to cleanup project dependencies for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to cleanup project dependencies"));
            }

            _logger.LogInformation("Successfully cleaned up project dependencies for user {UserId}", userId);
            return Ok(ApiResponse<object>.SuccessResult(new { message = "Project dependencies cleaned up successfully" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up project dependencies for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while cleaning up project dependencies"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }


    private bool IsSystemAdmin()
    {
        return User.IsInRole(Roles.SystemAdmin);
    }
}