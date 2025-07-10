using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.TaskService.Data;
using ProjectManagementSystem.TaskService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.TaskService.Services;

public class TaskCommentService : ITaskCommentService
{
    private readonly TaskDbContext _context;
    private readonly ILogger<TaskCommentService> _logger;
    private readonly IUserService _userService;

    public TaskCommentService(TaskDbContext context, ILogger<TaskCommentService> logger, IUserService userService)
    {
        _context = context;
        _logger = logger;
        _userService = userService;
    }

    public async Task<PagedResult<TaskCommentDto>> GetTaskCommentsAsync(Guid taskId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Comments
            .Where(c => c.TaskId == taskId && c.IsActive)
            .OrderBy(c => c.CreatedAt);

        var totalCount = await query.CountAsync();
        var commentEntities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Convert entities to DTOs with user information
        var comments = new List<TaskCommentDto>();
        foreach (var comment in commentEntities)
        {
            var commentDto = await MapCommentToDtoAsync(comment);
            comments.Add(commentDto);
        }

        return new PagedResult<TaskCommentDto>
        {
            Items = comments,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TaskCommentDto?> GetCommentByIdAsync(int commentId)
    {
        var comment = await _context.Comments
            .Where(c => c.Id == commentId && c.IsActive)
            .FirstOrDefaultAsync();

        return comment != null ? await MapCommentToDtoAsync(comment) : null;
    }

    public async Task<TaskCommentDto> CreateCommentAsync(Guid taskId, CreateTaskCommentDto createCommentDto, int userId)
    {
        var comment = new Comment
        {
            Content = createCommentDto.Content,
            TaskId = taskId,
            UserId = userId
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Comment created for task {TaskId} by user {UserId}", taskId, userId);

        return await MapCommentToDtoAsync(comment);
    }

    public async Task<TaskCommentDto?> UpdateCommentAsync(int commentId, UpdateTaskCommentDto updateCommentDto)
    {
        var comment = await _context.Comments
            .Where(c => c.Id == commentId && c.IsActive)
            .FirstOrDefaultAsync();

        if (comment == null)
            return null;

        comment.Content = updateCommentDto.Content;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Comment {CommentId} updated", commentId);

        return await MapCommentToDtoAsync(comment);
    }

    public async Task<bool> DeleteCommentAsync(int commentId)
    {
        var comment = await _context.Comments
            .Where(c => c.Id == commentId && c.IsActive)
            .FirstOrDefaultAsync();

        if (comment == null)
            return false;

        comment.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Comment {CommentId} deleted (soft delete)", commentId);
        return true;
    }

    public async Task<bool> CanEditCommentAsync(int commentId, int userId)
    {
        return await _context.Comments
            .AnyAsync(c => c.Id == commentId && c.UserId == userId && c.IsActive);
    }

    private async Task<TaskCommentDto> MapCommentToDtoAsync(Comment comment)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(comment.UserId);
            
            return new TaskCommentDto
            {
                Id = comment.Id,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                TaskId = comment.TaskId,
                UserId = comment.UserId,
                User = user
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {UserId} for comment {CommentId}", comment.UserId, comment.Id);
            
            // Return comment without user info if user service fails
            return new TaskCommentDto
            {
                Id = comment.Id,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                TaskId = comment.TaskId,
                UserId = comment.UserId,
                User = null
            };
        }
    }
}