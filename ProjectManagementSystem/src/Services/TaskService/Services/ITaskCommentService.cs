using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.TaskService.Services;

public interface ITaskCommentService
{
    Task<PagedResult<TaskCommentDto>> GetTaskCommentsAsync(int taskId, int pageNumber = 1, int pageSize = 10);
    Task<TaskCommentDto?> GetCommentByIdAsync(int commentId);
    Task<TaskCommentDto> CreateCommentAsync(int taskId, CreateTaskCommentDto createCommentDto, int userId);
    Task<TaskCommentDto?> UpdateCommentAsync(int commentId, UpdateTaskCommentDto updateCommentDto);
    Task<bool> DeleteCommentAsync(int commentId);
    Task<bool> CanEditCommentAsync(int commentId, int userId);
}