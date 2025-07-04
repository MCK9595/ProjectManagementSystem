using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ProjectManagementSystem.OrganizationService.Data;
using ProjectManagementSystem.OrganizationService.Data.Entities;
using ProjectManagementSystem.OrganizationService.Services;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.OrganizationService.Tests.Services;

public class OrganizationServiceUserDeletionTests : IDisposable
{
    private readonly OrganizationDbContext _context;
    private readonly Mock<ILogger<ProjectManagementSystem.OrganizationService.Services.OrganizationService>> _mockLogger;
    private readonly ProjectManagementSystem.OrganizationService.Services.OrganizationService _service;

    public OrganizationServiceUserDeletionTests()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new OrganizationDbContext(options);
        _mockLogger = new Mock<ILogger<ProjectManagementSystem.OrganizationService.Services.OrganizationService>>();
        _service = new ProjectManagementSystem.OrganizationService.Services.OrganizationService(_context, _mockLogger.Object);
    }

    [Fact]
    public async Task HasUserBlockingAdminRolesAsync_UserIsSoleOwner_ReturnsTrue()
    {
        // Arrange
        var userId = 123;
        var organizationId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = Roles.OrganizationOwner,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(organization);
        _context.OrganizationUsers.Add(organizationUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasUserBlockingAdminRolesAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasUserBlockingAdminRolesAsync_UserIsSoleAdmin_ReturnsTrue()
    {
        // Arrange
        var userId = 123;
        var organizationId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = Roles.OrganizationAdmin,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(organization);
        _context.OrganizationUsers.Add(organizationUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasUserBlockingAdminRolesAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasUserBlockingAdminRolesAsync_UserIsNotSoleAdmin_ReturnsFalse()
    {
        // Arrange
        var userId1 = 123;
        var userId2 = 456;
        var organizationId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            IsActive = true,
            CreatedByUserId = userId1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organizationUser1 = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId1,
            Role = Roles.OrganizationAdmin,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        var organizationUser2 = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId2,
            Role = Roles.OrganizationAdmin,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(organization);
        _context.OrganizationUsers.AddRange(organizationUser1, organizationUser2);
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
        var organizationId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = Roles.OrganizationMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(organization);
        _context.OrganizationUsers.Add(organizationUser);
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
        var organizationId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = Roles.OrganizationMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(organization);
        _context.OrganizationUsers.Add(organizationUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify membership was deactivated
        var updatedMembership = await _context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.UserId == userId);
        Assert.NotNull(updatedMembership);
        Assert.False(updatedMembership.IsActive);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_UserHasBlockingRoles_ThrowsException()
    {
        // Arrange
        var userId = 123;
        var organizationId = Guid.NewGuid();

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = Roles.OrganizationOwner,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(organization);
        _context.OrganizationUsers.Add(organizationUser);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CleanupUserDependenciesAsync(userId));

        Assert.Contains("sole administrator", exception.Message);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_MultipleMemberships_DeactivatesAll()
    {
        // Arrange
        var userId = 123;
        var organizationId1 = Guid.NewGuid();
        var organizationId2 = Guid.NewGuid();

        var organization1 = new Organization
        {
            Id = organizationId1,
            Name = "Test Organization 1",
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organization2 = new Organization
        {
            Id = organizationId2,
            Name = "Test Organization 2",
            IsActive = true,
            CreatedByUserId = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var organizationUser1 = new OrganizationUser
        {
            OrganizationId = organizationId1,
            UserId = userId,
            Role = Roles.OrganizationMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        var organizationUser2 = new OrganizationUser
        {
            OrganizationId = organizationId2,
            UserId = userId,
            Role = Roles.OrganizationMember,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.Organizations.AddRange(organization1, organization2);
        _context.OrganizationUsers.AddRange(organizationUser1, organizationUser2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result);

        // Verify all memberships were deactivated
        var memberships = await _context.OrganizationUsers
            .Where(ou => ou.UserId == userId)
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

    public void Dispose()
    {
        _context.Dispose();
    }
}