using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.WebApp.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>?> GetTasksAsync(int projectId, int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<TaskDto>?> GetTasksByUserAsync(int pageNumber = 1, int pageSize = 10);
    Task<TaskDto?> GetTaskAsync(int id);
    Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto);
    Task<TaskDto?> UpdateTaskAsync(int id, UpdateTaskDto taskDto);
    Task<bool> UpdateTaskStatusAsync(int id, string status);
    Task<bool> AssignTaskAsync(int id, int assignedToUserId);
    Task<bool> DeleteTaskAsync(int id);
}