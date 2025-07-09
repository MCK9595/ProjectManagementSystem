using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ProjectManagementSystem.ProjectService.Data;
using ProjectManagementSystem.ProjectService.Data.Entities;
using ProjectManagementSystem.ProjectService.Services;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.ProjectService.Tests.Services;

public class ProjectServiceUserDeletionTests : IDisposable
{
    private readonly ProjectDbContext _context;
    private readonly Mock<ILogger<ProjectManagementSystem.ProjectService.Services.ProjectService>> _mockLogger;
    private readonly Mock<IOrganizationService> _mockOrganizationService;
    private readonly ProjectManagementSystem.ProjectService.Services.ProjectService _service;

    public ProjectServiceUserDeletionTests()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ProjectDbContext(options);
        _mockLogger = new Mock<ILogger<ProjectManagementSystem.ProjectService.Services.ProjectService>>();
        _mockOrganizationService = new Mock<IOrganizationService>();
        _service = new ProjectManagementSystem.ProjectService.Services.ProjectService(_context, _mockLogger.Object, _mockOrganizationService.Object);
    }

    [Fact]
    public async Task HasUserBlockingAdminRolesAsync_UserIsSoleManager_ReturnsTrue()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            Name = "Test Project",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = Roles.ProjectManager,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasUserBlockingAdminRolesAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasUserBlockingAdminRolesAsync_UserIsNotSoleManager_ReturnsFalse()
    {
        // Arrange
        var userId1 = 123;
        var userId2 = 456;
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            Name = "Test Project",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = true,
            CreatedByUserId = userId1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectMember1 = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId1,
            Role = Roles.ProjectManager,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        var projectMember2 = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId2,
            Role = Roles.ProjectManager,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.AddRange(projectMember1, projectMember2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasUserBlockingAdminRolesAsync(userId1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasUserBlockingAdminRolesAsync_UserIsOnlyMember_ReturnsFalse()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            Name = "Test Project",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = Roles.ProjectMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasUserBlockingAdminRolesAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_NoBlockingRoles_ReturnsTrue()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            Name = "Test Project",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = Roles.ProjectMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify membership was deactivated
        var updatedMembership = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.UserId == userId);
        Assert.NotNull(updatedMembership);
        Assert.False(updatedMembership.IsActive);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_UserHasBlockingRoles_ThrowsException()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            Name = "Test Project",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = Roles.ProjectManager,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CleanupUserDependenciesAsync(userId));

        Assert.Contains("sole manager", exception.Message);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_MultipleMemberships_DeactivatesAll()
    {
        // Arrange
        var userId = 123;
        var projectId1 = Guid.NewGuid();
        var projectId2 = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var project1 = new Project
        {
            Id = projectId1,
            OrganizationId = organizationId,
            Name = "Test Project 1",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var project2 = new Project
        {
            Id = projectId2,
            OrganizationId = organizationId,
            Name = "Test Project 2",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectMember1 = new ProjectMember
        {
            ProjectId = projectId1,
            UserId = userId,
            Role = Roles.ProjectMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        var projectMember2 = new ProjectMember
        {
            ProjectId = projectId2,
            UserId = userId,
            Role = Roles.ProjectMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.AddRange(project1, project2);
        _context.ProjectMembers.AddRange(projectMember1, projectMember2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify all memberships were deactivated
        var memberships = await _context.ProjectMembers
            .Where(pm => pm.UserId == userId)
            .ToListAsync();

        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, m => Assert.False(m.IsActive));
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_NoMemberships_ReturnsTrue()
    {
        // Arrange
        var userId = 123;

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_InactiveProject_IgnoresProject()
    {
        // Arrange
        var userId = 123;
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            OrganizationId = organizationId,
            Name = "Test Project",
            Status = ProjectStatus.Active,
            Priority = Priority.Medium,
            IsActive = false, // Inactive project
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = Roles.ProjectManager,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert - Should succeed because inactive projects are ignored
        Assert.True(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}