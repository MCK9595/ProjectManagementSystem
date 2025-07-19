using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectManagementSystem.TaskService.Controllers;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ProjectManagementSystem.TaskService.Tests.Controllers;

public class TasksControllerDashboardStatsTests
{
    private readonly Mock<ProjectManagementSystem.TaskService.Services.ITaskService> _mockTaskService;
    private readonly Mock<ILogger<TasksController>> _mockLogger;
    private readonly TasksController _controller;
    private readonly Guid _testProjectId = Guid.NewGuid();

    public TasksControllerDashboardStatsTests()
    {
        _mockTaskService = new Mock<ProjectManagementSystem.TaskService.Services.ITaskService>();
        _mockLogger = new Mock<ILogger<TasksController>>();
        _controller = new TasksController(_mockTaskService.Object, _mockLogger.Object);

        // Setup user context
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Role, "SystemAdmin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task GetProjectDashboardStats_ValidProjectId_ReturnsSuccessResponse()
    {
        // Arrange
        var expectedStats = new ProjectDashboardStatsDto
        {
            StatusBreakdown = new StatusBreakdownDto
            {
                TodoCount = 5,
                InProgressCount = 3,
                InReviewCount = 2,
                DoneCount = 10,
                TotalCount = 20
            },
            PriorityBreakdown = new PriorityBreakdownDto
            {
                CriticalCount = 1,
                HighCount = 4,
                MediumCount = 10,
                LowCount = 5,
                TotalCount = 20
            },
            OverdueTasksCount = 2,
            TodayDueTasksCount = 1,
            CompletionRate = 50.0m,
            RecentActivities = new List<RecentTaskActivityDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Task",
                    Status = "InProgress",
                    Priority = "High",
                    ActivityType = "Created",
                    ActivityDate = DateTime.UtcNow.AddHours(-2),
                    AssignedToUserName = "John Doe"
                }
            }
        };

        _mockTaskService
            .Setup(x => x.GetProjectDashboardStatsAsync(_testProjectId, It.IsAny<int?>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetProjectDashboardStats(_testProjectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ProjectDashboardStatsDto>>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(expectedStats.StatusBreakdown.TotalCount, response.Data.StatusBreakdown.TotalCount);
        Assert.Equal(expectedStats.CompletionRate, response.Data.CompletionRate);
        Assert.Equal(expectedStats.OverdueTasksCount, response.Data.OverdueTasksCount);
        Assert.Equal(expectedStats.TodayDueTasksCount, response.Data.TodayDueTasksCount);
        Assert.Single(response.Data.RecentActivities);
        
        _mockTaskService.Verify(x => x.GetProjectDashboardStatsAsync(_testProjectId, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task GetProjectDashboardStats_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _mockTaskService
            .Setup(x => x.GetProjectDashboardStatsAsync(_testProjectId, It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.GetProjectDashboardStats(_testProjectId);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        
        var response = Assert.IsType<ApiResponse<ProjectDashboardStatsDto>>(statusCodeResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Internal server error", response.Message);
        
        _mockTaskService.Verify(x => x.GetProjectDashboardStatsAsync(_testProjectId, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task GetProjectDashboardStats_EmptyProject_ReturnsEmptyStats()
    {
        // Arrange
        var emptyStats = new ProjectDashboardStatsDto
        {
            StatusBreakdown = new StatusBreakdownDto { TotalCount = 0 },
            PriorityBreakdown = new PriorityBreakdownDto { TotalCount = 0 },
            OverdueTasksCount = 0,
            TodayDueTasksCount = 0,
            CompletionRate = 0,
            RecentActivities = new List<RecentTaskActivityDto>()
        };

        _mockTaskService
            .Setup(x => x.GetProjectDashboardStatsAsync(_testProjectId, It.IsAny<int?>()))
            .ReturnsAsync(emptyStats);

        // Act
        var result = await _controller.GetProjectDashboardStats(_testProjectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ProjectDashboardStatsDto>>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(0, response.Data.StatusBreakdown.TotalCount);
        Assert.Equal(0, response.Data.CompletionRate);
        Assert.Empty(response.Data.RecentActivities);
    }

    [Fact]
    public async Task GetProjectDashboardStats_ValidProjectWithAllStatuses_ReturnsCorrectBreakdown()
    {
        // Arrange
        var statsWithAllStatuses = new ProjectDashboardStatsDto
        {
            StatusBreakdown = new StatusBreakdownDto
            {
                TodoCount = 2,
                InProgressCount = 3,
                InReviewCount = 1,
                DoneCount = 4,
                CancelledCount = 1,
                TotalCount = 11
            },
            PriorityBreakdown = new PriorityBreakdownDto
            {
                CriticalCount = 2,
                HighCount = 3,
                MediumCount = 4,
                LowCount = 2,
                TotalCount = 11
            },
            OverdueTasksCount = 1,
            TodayDueTasksCount = 2,
            CompletionRate = 36.36m,
            RecentActivities = new List<RecentTaskActivityDto>()
        };

        _mockTaskService
            .Setup(x => x.GetProjectDashboardStatsAsync(_testProjectId, It.IsAny<int?>()))
            .ReturnsAsync(statsWithAllStatuses);

        // Act
        var result = await _controller.GetProjectDashboardStats(_testProjectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ProjectDashboardStatsDto>>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        
        // Verify status breakdown
        Assert.Equal(2, response.Data.StatusBreakdown.TodoCount);
        Assert.Equal(3, response.Data.StatusBreakdown.InProgressCount);
        Assert.Equal(1, response.Data.StatusBreakdown.InReviewCount);
        Assert.Equal(4, response.Data.StatusBreakdown.DoneCount);
        Assert.Equal(1, response.Data.StatusBreakdown.CancelledCount);
        Assert.Equal(11, response.Data.StatusBreakdown.TotalCount);
        
        // Verify priority breakdown
        Assert.Equal(2, response.Data.PriorityBreakdown.CriticalCount);
        Assert.Equal(3, response.Data.PriorityBreakdown.HighCount);
        Assert.Equal(4, response.Data.PriorityBreakdown.MediumCount);
        Assert.Equal(2, response.Data.PriorityBreakdown.LowCount);
        Assert.Equal(11, response.Data.PriorityBreakdown.TotalCount);
        
        // Verify other metrics
        Assert.Equal(1, response.Data.OverdueTasksCount);
        Assert.Equal(2, response.Data.TodayDueTasksCount);
        Assert.Equal(36.36m, response.Data.CompletionRate);
    }
}