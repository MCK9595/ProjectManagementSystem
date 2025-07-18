using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Shared.Common.Constants;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Security.Claims;

namespace ProjectManagementSystem.TaskService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly Services.ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(Services.ITaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [HttpGet("project/{projectId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetProjectTasks(
        Guid projectId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var tasks = await _taskService.GetTasksAsync(projectId, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResult<TaskDto>>.SuccessResult(tasks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<PagedResult<TaskDto>>.ErrorResult("Internal server error"));
        }
    }

    [HttpGet("user/{userId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetUserTasks(
        int userId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            // Users can only see their own tasks unless they're admin
            if (userId != currentUserId && !IsSystemAdmin())
            {
                return Forbid();
            }

            var tasks = await _taskService.GetTasksByUserAsync(userId, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResult<TaskDto>>.SuccessResult(tasks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks for user {UserId}", userId);
            return StatusCode(500, ApiResponse<PagedResult<TaskDto>>.ErrorResult("Internal server error"));
        }
    }

    [HttpGet("my-tasks")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetMyTasks(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            _logger.LogInformation("Getting tasks for current user {UserId}", currentUserId);

            var tasks = await _taskService.GetTasksByUserAsync(currentUserId, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResult<TaskDto>>.SuccessResult(tasks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks for current user");
            return StatusCode(500, ApiResponse<PagedResult<TaskDto>>.ErrorResult("Internal server error"));
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<TaskDto>>> GetTask(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _taskService.HasTaskAccessAsync(id, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var task = await _taskService.GetTaskByIdAsync(id);
            if (task == null)
            {
                return NotFound(ApiResponse<TaskDto>.ErrorResult("Task not found"));
            }

            return Ok(ApiResponse<TaskDto>.SuccessResult(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task {TaskId}", id);
            return StatusCode(500, ApiResponse<TaskDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<TaskDto>>> CreateTask([FromBody] CreateTaskDto createTaskDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<TaskDto>.ErrorResult("Invalid task data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.CreateTaskAsync(createTaskDto, userId);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, ApiResponse<TaskDto>.SuccessResult(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, ApiResponse<TaskDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<TaskDto>>> UpdateTask(Guid id, [FromBody] UpdateTaskDto updateTaskDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<TaskDto>.ErrorResult("Invalid task data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _taskService.CanEditTaskAsync(id, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var task = await _taskService.UpdateTaskAsync(id, updateTaskDto);
            if (task == null)
            {
                return NotFound(ApiResponse<TaskDto>.ErrorResult("Task not found"));
            }

            return Ok(ApiResponse<TaskDto>.SuccessResult(task));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId}", id);
            return StatusCode(500, ApiResponse<TaskDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateTaskStatus(Guid id, [FromBody] UpdateTaskStatusDto statusDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid status data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _taskService.CanEditTaskAsync(id, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _taskService.UpdateTaskStatusAsync(id, statusDto.Status);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Task not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Task status updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task status {TaskId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
        }
    }

    [HttpPatch("{id}/assign")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<object>>> AssignTask(Guid id, [FromBody] AssignTaskDto assignDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid assignment data"));
        }

        try
        {
            var success = await _taskService.AssignTaskAsync(id, assignDto.AssignedToUserId);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Task not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Task assigned successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning task {TaskId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTask(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _taskService.CanEditTaskAsync(id, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _taskService.DeleteTaskAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Task not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Task deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task {TaskId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// Clean up all task dependencies for a user (for deletion process)
    /// Note: TaskService doesn't have admin roles that would block deletion like organization/project admins
    /// </summary>
    [HttpDelete("user/{userId:int}/dependencies")]
    [AllowAnonymous] // Allow internal service calls
    public async Task<ActionResult<ApiResponse<object>>> CleanupUserDependencies(int userId)
    {
        try
        {
            _logger.LogInformation("Cleaning up task dependencies for user deletion - UserId: {UserId}", userId);

            var success = await _taskService.CleanupUserDependenciesAsync(userId);
            
            if (!success)
            {
                _logger.LogError("Failed to cleanup task dependencies for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to cleanup task dependencies"));
            }

            _logger.LogInformation("Successfully cleaned up task dependencies for user {UserId}", userId);
            return Ok(ApiResponse<object>.SuccessResult(new { message = "Task dependencies cleaned up successfully" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up task dependencies for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred while cleaning up task dependencies"));
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

public class UpdateTaskStatusDto
{
    public required string Status { get; set; }
}

public class AssignTaskDto
{
    public int AssignedToUserId { get; set; }
}