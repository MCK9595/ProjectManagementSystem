using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.TaskService.Data;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Constants;
using TaskStatusConstants = ProjectManagementSystem.Shared.Common.Constants.TaskStatus;

namespace ProjectManagementSystem.TaskService.Services;

public class TaskService : ITaskService
{
    private readonly TaskDbContext _context;
    private readonly ILogger<TaskService> _logger;

    public TaskService(TaskDbContext context, ILogger<TaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<TaskDto>> GetTasksAsync(int projectId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Tasks
            .Where(t => t.ProjectId == projectId && t.IsActive)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var tasks = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => MapTaskToDto(t))
            .ToListAsync();

        return new PagedResult<TaskDto>
        {
            Items = tasks,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<TaskDto>> GetTasksByUserAsync(int userId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Tasks
            .Where(t => t.AssignedToUserId == userId && t.IsActive)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var tasks = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => MapTaskToDto(t))
            .ToListAsync();

        return new PagedResult<TaskDto>
        {
            Items = tasks,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TaskDto?> GetTaskByIdAsync(int taskId)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        return task != null ? MapTaskToDto(task) : null;
    }

    public async Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto, int createdByUserId)
    {
        var task = new Data.Entities.Task
        {
            Title = createTaskDto.Title,
            Description = createTaskDto.Description,
            Status = createTaskDto.Status,
            Priority = createTaskDto.Priority,
            StartDate = createTaskDto.StartDate,
            DueDate = createTaskDto.DueDate,
            EstimatedHours = createTaskDto.EstimatedHours,
            ProjectId = createTaskDto.ProjectId,
            AssignedToUserId = createTaskDto.AssignedToUserId,
            CreatedByUserId = createdByUserId
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskTitle} created by user {UserId}", task.Title, createdByUserId);

        return MapTaskToDto(task);
    }

    public async Task<TaskDto?> UpdateTaskAsync(int taskId, UpdateTaskDto updateTaskDto)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        if (task == null)
            return null;

        if (!string.IsNullOrEmpty(updateTaskDto.Title))
            task.Title = updateTaskDto.Title;

        if (updateTaskDto.Description != null)
            task.Description = updateTaskDto.Description;

        if (!string.IsNullOrEmpty(updateTaskDto.Status))
        {
            task.Status = updateTaskDto.Status;
            if (updateTaskDto.Status == TaskStatusConstants.Done)
                task.CompletedDate = DateTime.UtcNow;
            else if (task.Status != TaskStatusConstants.Done)
                task.CompletedDate = null;
        }

        if (!string.IsNullOrEmpty(updateTaskDto.Priority))
            task.Priority = updateTaskDto.Priority;

        if (updateTaskDto.StartDate.HasValue)
            task.StartDate = updateTaskDto.StartDate;

        if (updateTaskDto.DueDate.HasValue)
            task.DueDate = updateTaskDto.DueDate;

        if (updateTaskDto.EstimatedHours.HasValue)
            task.EstimatedHours = updateTaskDto.EstimatedHours;

        if (updateTaskDto.ActualHours.HasValue)
            task.ActualHours = updateTaskDto.ActualHours;

        if (updateTaskDto.AssignedToUserId.HasValue)
            task.AssignedToUserId = updateTaskDto.AssignedToUserId.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} updated", taskId);

        return MapTaskToDto(task);
    }

    public async Task<bool> UpdateTaskStatusAsync(int taskId, string status)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        if (task == null)
            return false;

        task.Status = status;
        if (status == TaskStatusConstants.Done)
            task.CompletedDate = DateTime.UtcNow;
        else if (task.Status != TaskStatusConstants.Done)
            task.CompletedDate = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} status updated to {Status}", taskId, status);
        return true;
    }

    public async Task<bool> AssignTaskAsync(int taskId, int assignedToUserId)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        if (task == null)
            return false;

        task.AssignedToUserId = assignedToUserId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} assigned to user {UserId}", taskId, assignedToUserId);
        return true;
    }

    public async Task<bool> DeleteTaskAsync(int taskId)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        if (task == null)
            return false;

        task.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} deleted (soft delete)", taskId);
        return true;
    }

    public async Task<bool> HasTaskAccessAsync(int taskId, int userId)
    {
        return await _context.Tasks
            .AnyAsync(t => t.Id == taskId && 
                          (t.AssignedToUserId == userId || t.CreatedByUserId == userId) && 
                          t.IsActive);
    }

    public async Task<bool> CanEditTaskAsync(int taskId, int userId)
    {
        return await _context.Tasks
            .AnyAsync(t => t.Id == taskId && 
                          (t.AssignedToUserId == userId || t.CreatedByUserId == userId) && 
                          t.IsActive);
    }

    private static TaskDto MapTaskToDto(Data.Entities.Task task)
    {
        return new TaskDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CompletedDate = task.CompletedDate,
            EstimatedHours = task.EstimatedHours,
            ActualHours = task.ActualHours,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            ProjectId = task.ProjectId,
            CreatedByUserId = task.CreatedByUserId,
            AssignedToUserId = task.AssignedToUserId
        };
    }
}