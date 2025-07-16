using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.TaskService.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetTasksAsync(Guid projectId, int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<TaskDto>> GetTasksByUserAsync(int userId, int pageNumber = 1, int pageSize = 10);
    Task<TaskDto?> GetTaskByIdAsync(Guid taskId);
    Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto, int createdByUserId);
    Task<TaskDto?> UpdateTaskAsync(Guid taskId, UpdateTaskDto updateTaskDto);
    Task<bool> UpdateTaskStatusAsync(Guid taskId, string status);
    Task<bool> AssignTaskAsync(Guid taskId, int assignedToUserId);
    Task<bool> DeleteTaskAsync(Guid taskId);
    Task<bool> HasTaskAccessAsync(Guid taskId, int userId);
    Task<bool> CanEditTaskAsync(Guid taskId, int userId);
    
    // Task hierarchy methods
    Task<IEnumerable<TaskDto>> GetSubTasksAsync(Guid parentTaskId);
    Task<TaskDto?> GetParentTaskAsync(Guid taskId);
    Task<bool> SetParentTaskAsync(Guid taskId, Guid? parentTaskId);
    
    // Task dependency methods
    Task<IEnumerable<TaskDependencyDto>> GetTaskDependenciesAsync(Guid taskId);
    Task<IEnumerable<TaskDependencyDto>> GetTaskDependentsAsync(Guid taskId);
    Task<TaskDependencyDto> CreateTaskDependencyAsync(CreateTaskDependencyDto createDto);
    Task<bool> DeleteTaskDependencyAsync(Guid dependencyId);
    Task<bool> HasCircularDependencyAsync(Guid taskId, Guid dependentOnTaskId);
    Task<bool> HasCircularHierarchyAsync(Guid taskId, Guid? parentTaskId);
    
    // User deletion support method
    Task<bool> CleanupUserDependenciesAsync(int userId);
}