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
        int organizationId,
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

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.HasProjectAccessAsync(id, userId))
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
            var project = await _projectService.CreateProjectAsync(createProjectDto, userId);
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
    public async Task<ActionResult<ApiResponse<ProjectDto>>> UpdateProject(int id, [FromBody] UpdateProjectDto updateProjectDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProjectDto>.ErrorResult("Invalid project data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.IsProjectManagerAsync(id, userId) && !IsSystemAdmin())
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
    public async Task<ActionResult<ApiResponse<object>>> DeleteProject(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.IsProjectManagerAsync(id, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _projectService.DeleteProjectAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Project deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
        }
    }

    [HttpPost("{id}/archive")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<object>>> ArchiveProject(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.IsProjectManagerAsync(id, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _projectService.ArchiveProjectAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Project archived successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving project {ProjectId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
        }
    }

    [HttpPost("{id}/restore")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<object>>> RestoreProject(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.IsProjectManagerAsync(id, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _projectService.RestoreProjectAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Project not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Project restored successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring project {ProjectId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    private bool IsSystemAdmin()
    {
        return User.IsInRole(Roles.SystemAdmin);
    }
}