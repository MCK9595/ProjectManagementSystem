using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.WebApp.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>?> GetTasksAsync(Guid projectId, int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<TaskDto>?> GetTasksByUserAsync(int pageNumber = 1, int pageSize = 10);
    Task<TaskDto?> GetTaskAsync(Guid id);
    Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto);
    Task<TaskDto?> UpdateTaskAsync(Guid id, UpdateTaskDto taskDto);
    Task<bool> UpdateTaskStatusAsync(Guid id, string status);
    Task<bool> AssignTaskAsync(Guid id, int assignedToUserId);
    Task<bool> DeleteTaskAsync(Guid id);
    
    // コメント関連メソッド
    Task<PagedResult<TaskCommentDto>?> GetTaskCommentsAsync(Guid taskId, int pageNumber = 1, int pageSize = 10);
    Task<TaskCommentDto?> CreateTaskCommentAsync(Guid taskId, CreateTaskCommentDto commentDto);
    Task<TaskCommentDto?> UpdateTaskCommentAsync(int commentId, UpdateTaskCommentDto commentDto, Guid taskId);
    Task<bool> DeleteTaskCommentAsync(int commentId, Guid taskId);
}