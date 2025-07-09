using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ProjectManagementSystem.TaskService.Data;
using ProjectManagementSystem.TaskService.Data.Entities;
using ProjectManagementSystem.TaskService.Services;
using TaskEntity = ProjectManagementSystem.TaskService.Data.Entities.Task;

namespace ProjectManagementSystem.TaskService.Tests.Services;

public class TaskServiceUserDeletionTests : IDisposable
{
    private readonly TaskDbContext _context;
    private readonly Mock<ILogger<ProjectManagementSystem.TaskService.Services.TaskService>> _mockLogger;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly ProjectManagementSystem.TaskService.Services.TaskService _service;

    public TaskServiceUserDeletionTests()
    {
        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new TaskDbContext(options);
        _mockLogger = new Mock<ILogger<ProjectManagementSystem.TaskService.Services.TaskService>>();
        _mockUserService = new Mock<IUserService>();
        _mockProjectService = new Mock<IProjectService>();
        _service = new ProjectManagementSystem.TaskService.Services.TaskService(_context, _mockLogger.Object, _mockUserService.Object, _mockProjectService.Object);
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupUserDependenciesAsync_UserHasAssignedTasks_UnassignsUser()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var task = new TaskEntity
        {
            Id = taskId,
            TaskNumber = 1,
            Title = "Test Task",
            ProjectId = projectId,
            AssignedToUserId = userId,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify task was unassigned
        var updatedTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Null(updatedTask.AssignedToUserId);
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupUserDependenciesAsync_UserHasComments_PreservesComments()
    {
        // Arrange
        var userId = 123;
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var task = new TaskEntity
        {
            Id = taskId,
            TaskNumber = 1,
            Title = "Test Task",
            ProjectId = projectId,
            AssignedToUserId = 999,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var comment = new Comment
        {
            TaskId = taskId,
            UserId = userId,
            Content = "Test comment",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify comment is preserved
        var updatedComment = await _context.Comments.FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.NotNull(updatedComment);
        Assert.Equal("Test comment", updatedComment.Content);
        Assert.Equal(userId, updatedComment.UserId);
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupUserDependenciesAsync_UserHasMultipleTasks_UnassignsAll()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var taskId1 = Guid.NewGuid();
        var taskId2 = Guid.NewGuid();

        var task1 = new TaskEntity
        {
            Id = taskId1,
            TaskNumber = 1,
            Title = "Test Task 1",
            ProjectId = projectId,
            AssignedToUserId = userId,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var task2 = new TaskEntity
        {
            Id = taskId2,
            TaskNumber = 2,
            Title = "Test Task 2",
            ProjectId = projectId,
            AssignedToUserId = userId,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.AddRange(task1, task2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify all tasks were unassigned
        var tasks = await _context.Tasks
            .Where(t => t.Id == taskId1 || t.Id == taskId2)
            .ToListAsync();

        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, t => Assert.Null(t.AssignedToUserId));
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupUserDependenciesAsync_UserHasMultipleComments_PreservesAll()
    {
        // Arrange
        var userId = 123;
        var taskId1 = Guid.NewGuid();
        var taskId2 = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var task1 = new TaskEntity
        {
            Id = taskId1,
            TaskNumber = 1,
            Title = "Test Task 1",
            ProjectId = projectId,
            AssignedToUserId = 999,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var task2 = new TaskEntity
        {
            Id = taskId2,
            TaskNumber = 2,
            Title = "Test Task 2",
            ProjectId = projectId,
            AssignedToUserId = 999,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var comment1 = new Comment
        {
            TaskId = taskId1,
            UserId = userId,
            Content = "Test comment 1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var comment2 = new Comment
        {
            TaskId = taskId2,
            UserId = userId,
            Content = "Test comment 2",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.AddRange(task1, task2);
        _context.Comments.AddRange(comment1, comment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify all comments are preserved
        var comments = await _context.Comments
            .Where(c => c.UserId == userId)
            .ToListAsync();

        Assert.Equal(2, comments.Count);
        Assert.All(comments, c => Assert.Equal(userId, c.UserId));
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupUserDependenciesAsync_InactiveTasks_IgnoresInactiveTasks()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var task = new TaskEntity
        {
            Id = taskId,
            TaskNumber = 1,
            Title = "Test Task",
            ProjectId = projectId,
            AssignedToUserId = userId,
            CreatedByUserId = 999,
            IsActive = false, // Inactive task
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify inactive task was not modified
        var updatedTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(userId, updatedTask.AssignedToUserId); // Should remain assigned
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupUserDependenciesAsync_NoTasksOrComments_ReturnsTrue()
    {
        // Arrange
        var userId = 123;

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupUserDependenciesAsync_MixedAssignments_OnlyUnassignsUserTasks()
    {
        // Arrange
        var userId = 123;
        var otherUserId = 456;
        var projectId = Guid.NewGuid();
        var taskId1 = Guid.NewGuid();
        var taskId2 = Guid.NewGuid();

        var task1 = new TaskEntity
        {
            Id = taskId1,
            TaskNumber = 1,
            Title = "User Task",
            ProjectId = projectId,
            AssignedToUserId = userId,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var task2 = new TaskEntity
        {
            Id = taskId2,
            TaskNumber = 2,
            Title = "Other User Task",
            ProjectId = projectId,
            AssignedToUserId = otherUserId,
            CreatedByUserId = 999,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.AddRange(task1, task2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify only user's task was unassigned
        var userTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId1);
        var otherTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId2);
        
        Assert.NotNull(userTask);
        Assert.NotNull(otherTask);
        Assert.Null(userTask.AssignedToUserId);
        Assert.Equal(otherUserId, otherTask.AssignedToUserId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}