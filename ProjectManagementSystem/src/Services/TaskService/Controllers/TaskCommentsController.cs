using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.TaskService.Services;
using ProjectManagementSystem.Shared.Common.Constants;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Security.Claims;

namespace ProjectManagementSystem.TaskService.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/comments")]
[Authorize]
public class TaskCommentsController : ControllerBase
{
    private readonly ITaskCommentService _commentService;
    private readonly Services.ITaskService _taskService;
    private readonly ILogger<TaskCommentsController> _logger;

    public TaskCommentsController(
        ITaskCommentService commentService,
        Services.ITaskService taskService,
        ILogger<TaskCommentsController> logger)
    {
        _commentService = commentService;
        _taskService = taskService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<PagedResult<TaskCommentDto>>>> GetTaskComments(
        int taskId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _taskService.HasTaskAccessAsync(taskId, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var comments = await _commentService.GetTaskCommentsAsync(taskId, pageNumber, pageSize);
            return Ok(ApiResponse<PagedResult<TaskCommentDto>>.SuccessResult(comments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for task {TaskId}", taskId);
            return StatusCode(500, ApiResponse<PagedResult<TaskCommentDto>>.ErrorResult("Internal server error"));
        }
    }

    [HttpGet("{commentId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<TaskCommentDto>>> GetComment(int taskId, int commentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _taskService.HasTaskAccessAsync(taskId, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var comment = await _commentService.GetCommentByIdAsync(commentId);
            if (comment == null)
            {
                return NotFound(ApiResponse<TaskCommentDto>.ErrorResult("Comment not found"));
            }

            return Ok(ApiResponse<TaskCommentDto>.SuccessResult(comment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comment {CommentId} for task {TaskId}", commentId, taskId);
            return StatusCode(500, ApiResponse<TaskCommentDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<TaskCommentDto>>> CreateComment(
        int taskId,
        [FromBody] CreateTaskCommentDto createCommentDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<TaskCommentDto>.ErrorResult("Invalid comment data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _taskService.HasTaskAccessAsync(taskId, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var comment = await _commentService.CreateCommentAsync(taskId, createCommentDto, userId);
            return CreatedAtAction(nameof(GetComment), 
                new { taskId, commentId = comment.Id }, 
                ApiResponse<TaskCommentDto>.SuccessResult(comment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment for task {TaskId}", taskId);
            return StatusCode(500, ApiResponse<TaskCommentDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpPut("{commentId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.OrganizationMember},{Roles.ProjectManager},{Roles.ProjectMember}")]
    public async Task<ActionResult<ApiResponse<TaskCommentDto>>> UpdateComment(
        int taskId,
        int commentId,
        [FromBody] UpdateTaskCommentDto updateCommentDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<TaskCommentDto>.ErrorResult("Invalid comment data"));
        }

        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _commentService.CanEditCommentAsync(commentId, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var comment = await _commentService.UpdateCommentAsync(commentId, updateCommentDto);
            if (comment == null)
            {
                return NotFound(ApiResponse<TaskCommentDto>.ErrorResult("Comment not found"));
            }

            return Ok(ApiResponse<TaskCommentDto>.SuccessResult(comment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating comment {CommentId} for task {TaskId}", commentId, taskId);
            return StatusCode(500, ApiResponse<TaskCommentDto>.ErrorResult("Internal server error"));
        }
    }

    [HttpDelete("{commentId}")]
    [Authorize(Roles = $"{Roles.SystemAdmin},{Roles.OrganizationOwner},{Roles.ProjectManager}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteComment(int taskId, int commentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (!await _commentService.CanEditCommentAsync(commentId, userId) && !IsSystemAdmin())
            {
                return Forbid();
            }

            var success = await _commentService.DeleteCommentAsync(commentId);
            if (!success)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Comment not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Comment deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {CommentId} for task {TaskId}", commentId, taskId);
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