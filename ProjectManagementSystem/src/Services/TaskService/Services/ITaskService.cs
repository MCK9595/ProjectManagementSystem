using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.TaskService.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetTasksAsync(int projectId, int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<TaskDto>> GetTasksByUserAsync(int userId, int pageNumber = 1, int pageSize = 10);
    Task<TaskDto?> GetTaskByIdAsync(int taskId);
    Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto, int createdByUserId);
    Task<TaskDto?> UpdateTaskAsync(int taskId, UpdateTaskDto updateTaskDto);
    Task<bool> UpdateTaskStatusAsync(int taskId, string status);
    Task<bool> AssignTaskAsync(int taskId, int assignedToUserId);
    Task<bool> DeleteTaskAsync(int taskId);
    Task<bool> HasTaskAccessAsync(int taskId, int userId);
    Task<bool> CanEditTaskAsync(int taskId, int userId);
}