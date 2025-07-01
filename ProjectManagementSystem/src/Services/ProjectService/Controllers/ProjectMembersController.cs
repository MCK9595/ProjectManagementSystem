using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.ProjectService.Services;
using ProjectManagementSystem.Shared.Common.Constants;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Security.Claims;

namespace ProjectManagementSystem.ProjectService.Controllers;

[ApiController]
[Route("api/projects/{projectId}/members")]
[Authorize]
public class ProjectMembersController : ControllerBase
{
    private readonly IProjectMemberService _projectMemberService;
    private readonly Services.IProjectService _projectService;
    private readonly ILogger<ProjectMembersController> _logger;

    public ProjectMembersController(
        IProjectMemberService projectMemberService,
        Services.IProjectService projectService,
        ILogger<ProjectMembersController> logger)
    {
        _projectMemberService = projectMemberService;
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectMemberDto>>>> GetProjectMembers(
        Guid projectId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.HasProjectAccessAsync(projectId, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var members = await _projectMemberService.GetProjectMembersAsync(projectId, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResult<ProjectMemberDto>>.SuccessResult(members));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<PagedResult<ProjectMemberDto>>.ErrorResult("Internal server error"));
        }
    }

    [HttpGet("{userId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<ProjectMemberDto>>> GetProjectMember(Guid projectId, int userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            if (!await _projectService.HasProjectAccessAsync(projectId, currentUserId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var member = await _projectMemberService.GetProjectMemberAsync(projectId, userId);
            if (member == null)
            {
                return NotFound(ApiResponse<ProjectMemberDto>.ErrorResult("Project member not found"));
            }

            return Ok(ApiResponse<ProjectMemberDto>.SuccessResult(member));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project member {UserId} for project {ProjectId}", userId, projectId);
            return StatusCode(500, ApiResponse<ProjectMemberDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<ProjectMemberDto>>> AddProjectMember(
        Guid projectId,
        [FromBody] AddProjectMemberDto addMemberDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProjectMemberDto>.ErrorResult("Invalid member data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _projectService.IsProjectManagerAsync(projectId, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var member = await _projectMemberService.AddProjectMemberAsync(projectId, addMemberDto.UserId, addMemberDto.Role);
            return CreatedAtAction(nameof(GetProjectMember), 
                new { projectId, userId = member.UserId }, 
                ApiResponse<ProjectMemberDto>.SuccessResult(member));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ProjectMemberDto>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<ProjectMemberDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPut("{userId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<ProjectMemberDto>>> UpdateProjectMemberRole(
        Guid projectId,
        int userId,
        [FromBody] UpdateProjectMemberDto updateMemberDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<ProjectMemberDto>.ErrorResult("Invalid member data"));
        }

        try
        {
            var currentUserId = GetCurrentUserId();
            
            if (!await _projectService.IsProjectManagerAsync(projectId, currentUserId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var member = await _projectMemberService.UpdateProjectMemberRoleAsync(projectId, userId, updateMemberDto.Role);
            if (member == null)
            {
                return NotFound(ApiResponse<ProjectMemberDto>.ErrorResult("Project member not found"));
            }

            return Ok(ApiResponse<ProjectMemberDto>.SuccessResult(member));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member role for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<ProjectMemberDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpDelete("{userId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveProjectMember(Guid projectId, int userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            if (!await _projectService.IsProjectManagerAsync(projectId, currentUserId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _projectMemberService.RemoveProjectMemberAsync(projectId, userId);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Project member not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Project member removed successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from project {ProjectId}", projectId);
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

public class AddProjectMemberDto
{
    public int UserId { get; set; }
    public required string Role { get; set; }
}

public class UpdateProjectMemberDto
{
    public required string Role { get; set; }
}