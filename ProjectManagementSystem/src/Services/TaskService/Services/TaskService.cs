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
    private readonly IUserService _userService;
    private readonly IProjectService _projectService;

    public TaskService(TaskDbContext context, ILogger<TaskService> logger, IUserService userService, IProjectService projectService)
    {
        _context = context;
        _logger = logger;
        _userService = userService;
        _projectService = projectService;
    }

    public async Task<PagedResult<TaskDto>> GetTasksAsync(Guid projectId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Tasks
            .Where(t => t.ProjectId == projectId && t.IsActive)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var taskEntities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Convert each task entity to DTO with related data
        var tasks = new List<TaskDto>();
        foreach (var taskEntity in taskEntities)
        {
            var taskDto = await MapTaskToDtoAsync(taskEntity);
            tasks.Add(taskDto);
        }

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
        var taskEntities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Convert each task entity to DTO with related data
        var tasks = new List<TaskDto>();
        foreach (var taskEntity in taskEntities)
        {
            var taskDto = await MapTaskToDtoAsync(taskEntity);
            tasks.Add(taskDto);
        }

        return new PagedResult<TaskDto>
        {
            Items = tasks,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<TaskDto?> GetTaskByIdAsync(Guid taskId)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        return task != null ? await MapTaskToDtoAsync(task) : null;
    }

    public async Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto, int createdByUserId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // プロジェクト内の最大タスク番号を取得
                var maxTaskNumber = await _context.Tasks
                    .Where(t => t.ProjectId == createTaskDto.ProjectId && t.IsActive)
                    .MaxAsync(t => (int?)t.TaskNumber) ?? 0;

                var task = new Data.Entities.Task
                {
                    TaskNumber = maxTaskNumber + 1,
                    Title = createTaskDto.Title,
                    Description = createTaskDto.Description,
                    Status = createTaskDto.Status,
                    Priority = createTaskDto.Priority,
                    StartDate = createTaskDto.StartDate,
                    DueDate = createTaskDto.DueDate,
                    EstimatedHours = createTaskDto.EstimatedHours,
                    ProjectId = createTaskDto.ProjectId,
                    AssignedToUserId = createTaskDto.AssignedToUserId,
                    ParentTaskId = createTaskDto.ParentTaskId,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                // Create task dependencies if specified
                if (createTaskDto.DependsOnTaskIds.Any())
                {
                    var dependencies = createTaskDto.DependsOnTaskIds.Select(dependsOnTaskId => 
                        new Data.Entities.TaskDependency
                        {
                            TaskId = task.Id,
                            DependentOnTaskId = dependsOnTaskId,
                            CreatedAt = DateTime.UtcNow
                        });

                    _context.TaskDependencies.AddRange(dependencies);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Task {TaskTitle} (#{TaskNumber}) created by user {UserId}", 
                    task.Title, task.TaskNumber, createdByUserId);

                return await MapTaskToDtoAsync(task);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create task {TaskTitle}", createTaskDto.Title);
                throw;
            }
        });
    }

    public async Task<TaskDto?> UpdateTaskAsync(Guid taskId, UpdateTaskDto updateTaskDto)
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

        if (updateTaskDto.ParentTaskId.HasValue)
            task.ParentTaskId = updateTaskDto.ParentTaskId.Value;

        // Update dependencies if specified
        if (updateTaskDto.DependsOnTaskIds != null)
        {
            // Remove existing dependencies
            var existingDependencies = _context.TaskDependencies
                .Where(d => d.TaskId == taskId);
            _context.TaskDependencies.RemoveRange(existingDependencies);

            // Add new dependencies
            var newDependencies = updateTaskDto.DependsOnTaskIds.Select(dependsOnTaskId => 
                new Data.Entities.TaskDependency
                {
                    TaskId = taskId,
                    DependentOnTaskId = dependsOnTaskId,
                    CreatedAt = DateTime.UtcNow
                });
            
            _context.TaskDependencies.AddRange(newDependencies);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} updated", taskId);

        return await MapTaskToDtoAsync(task);
    }

    public async Task<bool> UpdateTaskStatusAsync(Guid taskId, string status)
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

    public async Task<bool> AssignTaskAsync(Guid taskId, int assignedToUserId)
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

    public async Task<bool> DeleteTaskAsync(Guid taskId)
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

    public async Task<bool> HasTaskAccessAsync(Guid taskId, int userId)
    {
        return await _context.Tasks
            .AnyAsync(t => t.Id == taskId && 
                          (t.AssignedToUserId == userId || t.CreatedByUserId == userId) && 
                          t.IsActive);
    }

    public async Task<bool> CanEditTaskAsync(Guid taskId, int userId)
    {
        return await _context.Tasks
            .AnyAsync(t => t.Id == taskId && 
                          (t.AssignedToUserId == userId || t.CreatedByUserId == userId) && 
                          t.IsActive);
    }

    public async Task<bool> CleanupUserDependenciesAsync(int userId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                _logger.LogInformation("Starting task dependency cleanup for user {UserId}", userId);

                const int batchSize = 100;
                var totalTasksUpdated = 0;

                while (true)
                {
                    var assignedTasks = await _context.Tasks
                        .Where(t => t.AssignedToUserId == userId && t.IsActive)
                        .Take(batchSize)
                        .ToListAsync();

                    if (!assignedTasks.Any())
                    {
                        break;
                    }

                    foreach (var task in assignedTasks)
                    {
                        task.AssignedToUserId = null;
                        task.UpdatedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    totalTasksUpdated += assignedTasks.Count;
                }

                _logger.LogInformation("Successfully cleaned up task dependencies for user {UserId} - unassigned from {TaskCount} tasks", 
                    userId, totalTasksUpdated);

                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to cleanup task dependencies for user {UserId}", userId);
                throw;
            }
        });
    }

    private async Task<TaskDto> MapTaskToDtoAsync(Data.Entities.Task task)
    {
        try
        {
            // Fetch project and user information in parallel
            var projectTask = _projectService.GetProjectAsync(task.ProjectId);
            var assignedUserTask = task.AssignedToUserId.HasValue 
                ? _userService.GetUserByIdAsync(task.AssignedToUserId.Value) 
                : Task.FromResult<UserDto?>(null);

            await Task.WhenAll(projectTask, assignedUserTask);

            var project = await projectTask;
            var assignedUser = await assignedUserTask;

            return new TaskDto
            {
                Id = task.Id,
                TaskNumber = task.TaskNumber,
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
                AssignedToUserId = task.AssignedToUserId,
                ParentTaskId = task.ParentTaskId,
                Project = project,
                AssignedTo = assignedUser
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping task {TaskId} to DTO", task.Id);
            
            // Return basic task info if external service calls fail
            return new TaskDto
            {
                Id = task.Id,
                TaskNumber = task.TaskNumber,
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
                AssignedToUserId = task.AssignedToUserId,
                ParentTaskId = task.ParentTaskId
            };
        }
    }

    // Task hierarchy methods
    public async Task<IEnumerable<TaskDto>> GetSubTasksAsync(Guid parentTaskId)
    {
        var subTasks = await _context.Tasks
            .Where(t => t.ParentTaskId == parentTaskId && t.IsActive)
            .ToListAsync();

        var subTaskDtos = new List<TaskDto>();
        foreach (var subTask in subTasks)
        {
            subTaskDtos.Add(await MapTaskToDtoAsync(subTask));
        }

        return subTaskDtos;
    }

    public async Task<TaskDto?> GetParentTaskAsync(Guid taskId)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        if (task?.ParentTaskId == null)
            return null;

        var parentTask = await _context.Tasks
            .Where(t => t.Id == task.ParentTaskId && t.IsActive)
            .FirstOrDefaultAsync();

        return parentTask != null ? await MapTaskToDtoAsync(parentTask) : null;
    }

    public async Task<bool> SetParentTaskAsync(Guid taskId, Guid? parentTaskId)
    {
        var task = await _context.Tasks
            .Where(t => t.Id == taskId && t.IsActive)
            .FirstOrDefaultAsync();

        if (task == null)
            return false;

        // Check for circular hierarchy
        if (parentTaskId.HasValue && await HasCircularHierarchyAsync(taskId, parentTaskId.Value))
        {
            _logger.LogWarning("Circular hierarchy detected for task {TaskId} with parent {ParentTaskId}", 
                taskId, parentTaskId.Value);
            return false;
        }

        task.ParentTaskId = parentTaskId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} parent set to {ParentTaskId}", taskId, parentTaskId);
        return true;
    }

    // Task dependency methods
    public async Task<IEnumerable<TaskDependencyDto>> GetTaskDependenciesAsync(Guid taskId)
    {
        var dependencies = await _context.TaskDependencies
            .Where(d => d.TaskId == taskId)
            .ToListAsync();

        var dependencyDtos = new List<TaskDependencyDto>();
        foreach (var dependency in dependencies)
        {
            var dependentOnTask = await _context.Tasks
                .Where(t => t.Id == dependency.DependentOnTaskId && t.IsActive)
                .FirstOrDefaultAsync();

            if (dependentOnTask != null)
            {
                dependencyDtos.Add(new TaskDependencyDto
                {
                    Id = dependency.Id,
                    TaskId = dependency.TaskId,
                    DependentOnTaskId = dependency.DependentOnTaskId,
                    CreatedAt = dependency.CreatedAt,
                    DependentOnTask = await MapTaskToDtoAsync(dependentOnTask)
                });
            }
        }

        return dependencyDtos;
    }

    public async Task<IEnumerable<TaskDependencyDto>> GetTaskDependentsAsync(Guid taskId)
    {
        var dependents = await _context.TaskDependencies
            .Where(d => d.DependentOnTaskId == taskId)
            .ToListAsync();

        var dependentDtos = new List<TaskDependencyDto>();
        foreach (var dependent in dependents)
        {
            var task = await _context.Tasks
                .Where(t => t.Id == dependent.TaskId && t.IsActive)
                .FirstOrDefaultAsync();

            if (task != null)
            {
                dependentDtos.Add(new TaskDependencyDto
                {
                    Id = dependent.Id,
                    TaskId = dependent.TaskId,
                    DependentOnTaskId = dependent.DependentOnTaskId,
                    CreatedAt = dependent.CreatedAt,
                    Task = await MapTaskToDtoAsync(task)
                });
            }
        }

        return dependentDtos;
    }

    public async Task<TaskDependencyDto> CreateTaskDependencyAsync(CreateTaskDependencyDto createDto)
    {
        // Check for circular dependency
        if (await HasCircularDependencyAsync(createDto.TaskId, createDto.DependentOnTaskId))
        {
            throw new InvalidOperationException("Creating this dependency would create a circular dependency");
        }

        var dependency = new Data.Entities.TaskDependency
        {
            TaskId = createDto.TaskId,
            DependentOnTaskId = createDto.DependentOnTaskId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskDependencies.Add(dependency);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task dependency created: Task {TaskId} depends on {DependentOnTaskId}", 
            createDto.TaskId, createDto.DependentOnTaskId);

        return new TaskDependencyDto
        {
            Id = dependency.Id,
            TaskId = dependency.TaskId,
            DependentOnTaskId = dependency.DependentOnTaskId,
            CreatedAt = dependency.CreatedAt
        };
    }

    public async Task<bool> DeleteTaskDependencyAsync(Guid dependencyId)
    {
        var dependency = await _context.TaskDependencies
            .Where(d => d.Id == dependencyId)
            .FirstOrDefaultAsync();

        if (dependency == null)
            return false;

        _context.TaskDependencies.Remove(dependency);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task dependency {DependencyId} deleted", dependencyId);
        return true;
    }

    public async Task<bool> HasCircularDependencyAsync(Guid taskId, Guid dependentOnTaskId)
    {
        // A task cannot depend on itself
        if (taskId == dependentOnTaskId)
            return true;

        // Check if dependentOnTaskId already depends on taskId (directly or indirectly)
        var visited = new HashSet<Guid>();
        var toVisit = new Stack<Guid>();
        toVisit.Push(dependentOnTaskId);

        while (toVisit.Count > 0)
        {
            var currentTaskId = toVisit.Pop();
            
            if (visited.Contains(currentTaskId))
                continue;
                
            visited.Add(currentTaskId);

            var dependencies = await _context.TaskDependencies
                .Where(d => d.TaskId == currentTaskId)
                .Select(d => d.DependentOnTaskId)
                .ToListAsync();

            foreach (var depId in dependencies)
            {
                if (depId == taskId)
                    return true;
                    
                if (!visited.Contains(depId))
                    toVisit.Push(depId);
            }
        }

        return false;
    }

    public async Task<bool> HasCircularHierarchyAsync(Guid taskId, Guid? parentTaskId)
    {
        if (!parentTaskId.HasValue)
            return false;

        // A task cannot be its own parent
        if (taskId == parentTaskId.Value)
            return true;

        // Check if parentTaskId is already a child of taskId (directly or indirectly)
        var visited = new HashSet<Guid>();
        var toVisit = new Stack<Guid>();
        toVisit.Push(parentTaskId.Value);

        while (toVisit.Count > 0)
        {
            var currentTaskId = toVisit.Pop();
            
            if (visited.Contains(currentTaskId))
                continue;
                
            visited.Add(currentTaskId);

            var parentTask = await _context.Tasks
                .Where(t => t.Id == currentTaskId && t.IsActive)
                .FirstOrDefaultAsync();

            if (parentTask?.ParentTaskId.HasValue == true)
            {
                if (parentTask.ParentTaskId.Value == taskId)
                    return true;
                    
                if (!visited.Contains(parentTask.ParentTaskId.Value))
                    toVisit.Push(parentTask.ParentTaskId.Value);
            }
        }

        return false;
    }

    public async Task<ProjectDashboardStatsDto> GetProjectDashboardStatsAsync(Guid projectId, int? currentUserId = null)
    {
        var allTasks = await _context.Tasks
            .Where(t => t.ProjectId == projectId && t.IsActive)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var today = now.Date;

        // Get project information for period filtering
        var project = await _projectService.GetProjectAsync(projectId);

        // Status breakdown
        var statusBreakdown = new StatusBreakdownDto
        {
            TodoCount = allTasks.Count(t => t.Status == TaskStatusConstants.ToDo),
            InProgressCount = allTasks.Count(t => t.Status == TaskStatusConstants.InProgress),
            InReviewCount = allTasks.Count(t => t.Status == TaskStatusConstants.InReview),
            DoneCount = allTasks.Count(t => t.Status == TaskStatusConstants.Done),
            CancelledCount = allTasks.Count(t => t.Status == TaskStatusConstants.Cancelled),
            TotalCount = allTasks.Count
        };

        // Priority breakdown
        var priorityBreakdown = new PriorityBreakdownDto
        {
            CriticalCount = allTasks.Count(t => t.Priority == Priority.Critical),
            HighCount = allTasks.Count(t => t.Priority == Priority.High),
            MediumCount = allTasks.Count(t => t.Priority == Priority.Medium),
            LowCount = allTasks.Count(t => t.Priority == Priority.Low),
            TotalCount = allTasks.Count
        };

        // Overdue tasks (due date passed and not completed)
        var overdueTasksCount = allTasks.Count(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date < today && 
            t.Status != TaskStatusConstants.Done && 
            t.Status != TaskStatusConstants.Cancelled);

        // Today due tasks
        var todayDueTasksCount = allTasks.Count(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date == today &&
            t.Status != TaskStatusConstants.Done && 
            t.Status != TaskStatusConstants.Cancelled);

        // Completion rate
        var completionRate = allTasks.Count > 0 
            ? (decimal)statusBreakdown.DoneCount / allTasks.Count * 100 
            : 0;

        // Recent activities (last 7 days)
        var sevenDaysAgo = now.AddDays(-7);
        var recentTasks = allTasks
            .Where(t => t.CreatedAt >= sevenDaysAgo || t.UpdatedAt >= sevenDaysAgo)
            .OrderByDescending(t => Math.Max(t.CreatedAt.Ticks, t.UpdatedAt.Ticks))
            .Take(10)
            .ToList();

        var recentActivities = new List<RecentTaskActivityDto>();
        foreach (var task in recentTasks)
        {
            string assignedToUserName = null;
            if (task.AssignedToUserId.HasValue)
            {
                try
                {
                    var user = await _userService.GetUserByIdAsync(task.AssignedToUserId.Value);
                    assignedToUserName = user?.Username;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user name for user {UserId}", task.AssignedToUserId.Value);
                }
            }

            var isRecentlyCreated = task.CreatedAt >= sevenDaysAgo;
            var isRecentlyUpdated = task.UpdatedAt >= sevenDaysAgo && task.UpdatedAt > task.CreatedAt;

            if (isRecentlyCreated)
            {
                recentActivities.Add(new RecentTaskActivityDto
                {
                    Id = task.Id,
                    Title = task.Title,
                    Status = task.Status,
                    Priority = task.Priority,
                    DueDate = task.DueDate,
                    ActivityType = "Created",
                    ActivityDate = task.CreatedAt,
                    AssignedToUserName = assignedToUserName
                });
            }
            else if (isRecentlyUpdated)
            {
                recentActivities.Add(new RecentTaskActivityDto
                {
                    Id = task.Id,
                    Title = task.Title,
                    Status = task.Status,
                    Priority = task.Priority,
                    DueDate = task.DueDate,
                    ActivityType = "Updated",
                    ActivityDate = task.UpdatedAt,
                    AssignedToUserName = assignedToUserName
                });
            }
        }

        // 具体的なタスクリストの生成
        var todayDueTasks = await CreateDashboardTaskListAsync(
            allTasks.Where(t => t.DueDate.HasValue && 
                               t.DueDate.Value.Date == today &&
                               t.Status != TaskStatusConstants.Done && 
                               t.Status != TaskStatusConstants.Cancelled)
                    .Take(10)
                    .ToList());

        var overdueTasks = await CreateDashboardTaskListAsync(
            allTasks.Where(t => t.DueDate.HasValue && 
                               t.DueDate.Value.Date < today && 
                               t.Status != TaskStatusConstants.Done && 
                               t.Status != TaskStatusConstants.Cancelled)
                    .OrderBy(t => t.DueDate)
                    .Take(10)
                    .ToList());

        // Set days overdue for overdue tasks
        foreach (var task in overdueTasks)
        {
            if (allTasks.FirstOrDefault(t => t.Id == task.Id)?.DueDate is DateTime dueDate)
            {
                task.DaysOverdue = (int)(today - dueDate.Date).TotalDays;
            }
        }

        // Active tasks in project period (not completed, within project timeframe)
        var activeTasksInPeriod = new List<DashboardTaskDto>();
        if (project?.StartDate.HasValue == true || project?.EndDate.HasValue == true)
        {
            var projectStart = project.StartDate?.Date;
            var projectEnd = project.EndDate?.Date;
            
            var periodTasks = allTasks.Where(t => 
            {
                // Filter out completed or cancelled tasks
                if (t.Status == TaskStatusConstants.Done || t.Status == TaskStatusConstants.Cancelled)
                    return false;
                
                // Determine the task's effective period
                var taskStart = t.StartDate?.Date;
                var taskEnd = t.DueDate?.Date;
                
                // Active tasks criteria:
                // 1. Task should be currently active (started but not finished)
                // 2. Should overlap with project period (if project has dates)
                
                // Check if task is currently active based on dates
                bool isCurrentlyActive = false;
                
                if (taskStart.HasValue && taskEnd.HasValue)
                {
                    // Task has both start and end dates
                    // Active if: start date has passed and end date hasn't passed
                    isCurrentlyActive = taskStart.Value <= today && taskEnd.Value >= today;
                }
                else if (taskStart.HasValue && !taskEnd.HasValue)
                {
                    // Task has only start date
                    // Active if: start date has passed
                    isCurrentlyActive = taskStart.Value <= today;
                }
                else if (!taskStart.HasValue && taskEnd.HasValue)
                {
                    // Task has only end date
                    // Active if: end date hasn't passed
                    isCurrentlyActive = taskEnd.Value >= today;
                }
                else
                {
                    // Task has no dates - consider active if status is not ToDo
                    isCurrentlyActive = t.Status != TaskStatusConstants.ToDo;
                }
                
                if (!isCurrentlyActive)
                    return false;
                
                // Check if task period overlaps with project period (if project has dates)
                if (projectStart.HasValue || projectEnd.HasValue)
                {
                    var taskEarliestDate = taskStart ?? taskEnd ?? today;
                    var taskLatestDate = taskEnd ?? taskStart ?? today;
                    
                    // Check overlap with project period
                    if (projectStart.HasValue && taskLatestDate < projectStart.Value)
                        return false; // Task ends before project starts
                        
                    if (projectEnd.HasValue && taskEarliestDate > projectEnd.Value)
                        return false; // Task starts after project ends
                }
                    
                return true; // Task is active and within project period
            })
                .OrderBy(t => t.DueDate ?? t.StartDate ?? DateTime.MaxValue)
                .Take(10)
                .ToList();
                
            activeTasksInPeriod = await CreateDashboardTaskListAsync(periodTasks);
        }

        // User assigned tasks (if currentUserId provided)
        var userAssignedTasks = new List<DashboardTaskDto>();
        if (currentUserId.HasValue)
        {
            var userTasks = allTasks.Where(t => 
                t.AssignedToUserId == currentUserId.Value &&
                t.Status != TaskStatusConstants.Done && 
                t.Status != TaskStatusConstants.Cancelled)
                .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
                .Take(10)
                .ToList();
                
            userAssignedTasks = await CreateDashboardTaskListAsync(userTasks);
        }

        return new ProjectDashboardStatsDto
        {
            StatusBreakdown = statusBreakdown,
            PriorityBreakdown = priorityBreakdown,
            OverdueTasksCount = overdueTasksCount,
            TodayDueTasksCount = todayDueTasksCount,
            CompletionRate = completionRate,
            RecentActivities = recentActivities.OrderByDescending(a => a.ActivityDate).ToList(),
            TodayDueTasks = todayDueTasks,
            OverdueTasks = overdueTasks,
            ActiveTasksInPeriod = activeTasksInPeriod,
            UserAssignedTasks = userAssignedTasks
        };
    }

    private async Task<List<DashboardTaskDto>> CreateDashboardTaskListAsync(List<Data.Entities.Task> tasks)
    {
        var dashboardTasks = new List<DashboardTaskDto>();
        
        foreach (var task in tasks)
        {
            string? assignedToUserName = null;
            if (task.AssignedToUserId.HasValue)
            {
                try
                {
                    var user = await _userService.GetUserByIdAsync(task.AssignedToUserId.Value);
                    assignedToUserName = user?.Username;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user name for user {UserId}", task.AssignedToUserId.Value);
                }
            }

            dashboardTasks.Add(new DashboardTaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Status = task.Status,
                Priority = task.Priority,
                DueDate = task.DueDate,
                StartDate = task.StartDate,
                AssignedToUserName = assignedToUserName,
                AssignedToUserId = task.AssignedToUserId,
                DaysOverdue = 0 // Will be set separately for overdue tasks
            });
        }

        return dashboardTasks;
    }
}