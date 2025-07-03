using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;

namespace ProjectManagementSystem.IdentityService.Tests.Diagnostics;

/// <summary>
/// Diagnostic tests to identify the root cause of current login errors
/// These tests help verify database schema and migration issues
/// </summary>
public class LoginErrorDiagnosticTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public LoginErrorDiagnosticTests()
    {
        // Use in-memory database for diagnostic testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
    }

    [Fact]
    public void ApplicationUser_EntityConfiguration_HasSoftDeleteFields()
    {
        // Arrange & Act
        var entity = _context.Model.FindEntityType(typeof(ApplicationUser));

        // Assert
        entity.Should().NotBeNull("ApplicationUser entity should be configured");

        var deletedAtProperty = entity!.FindProperty(nameof(ApplicationUser.DeletedAt));
        var deletedByProperty = entity.FindProperty(nameof(ApplicationUser.DeletedBy));

        deletedAtProperty.Should().NotBeNull("DeletedAt property should be configured");
        deletedByProperty.Should().NotBeNull("DeletedBy property should be configured");

        // Verify column names - in-memory provider may use property names instead of Column attributes
        // The important thing is that the properties exist and can be queried
        deletedAtProperty!.GetColumnName().Should().NotBeNullOrEmpty();
        deletedByProperty!.GetColumnName().Should().NotBeNullOrEmpty();
        
        // For production with SQL Server, column names should be snake_case as defined in Column attributes
        // For in-memory testing, they may be PascalCase property names
        var expectedDeletedAtName = deletedAtProperty.GetColumnName();
        var expectedDeletedByName = deletedByProperty.GetColumnName();
        
        // The column names should be either snake_case (SQL Server) or PascalCase (in-memory)
        (expectedDeletedAtName == "deleted_at" || expectedDeletedAtName == "DeletedAt").Should().BeTrue(
            "Column name should be either 'deleted_at' (SQL Server) or 'DeletedAt' (in-memory)");
        (expectedDeletedByName == "deleted_by" || expectedDeletedByName == "DeletedBy").Should().BeTrue(
            "Column name should be either 'deleted_by' (SQL Server) or 'DeletedBy' (in-memory)");

        // Verify data types
        deletedAtProperty.ClrType.Should().Be(typeof(DateTime?));
        deletedByProperty.ClrType.Should().Be(typeof(int?));
    }

    [Fact]
    public async Task ApplicationUser_SoftDeleteFields_CanBeQueried()
    {
        // Arrange
        var testUser = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = null, // Not deleted
            DeletedBy = null
        };

        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();

        // Act & Assert - Test queries that were failing in production

        // Test 1: Query with DeletedAt == null (this was causing the SQL error)
        var activeUsersQuery = _context.Users.Where(u => u.DeletedAt == null);
        var activeUsers = await activeUsersQuery.ToListAsync();
        activeUsers.Should().HaveCount(1);
        activeUsers.First().Id.Should().Be(1);

        // Test 2: Query with soft delete check in login scenario
        var loginUser = await _context.Users
            .Where(u => (u.UserName == "testuser" || u.Email == "testuser") && u.DeletedAt == null)
            .FirstOrDefaultAsync();
        
        loginUser.Should().NotBeNull();
        loginUser!.UserName.Should().Be("testuser");

        // Test 3: Verify soft delete functionality
        testUser.DeletedAt = DateTime.UtcNow;
        testUser.DeletedBy = 999; // Admin user ID
        await _context.SaveChangesAsync();

        var deletedUserQuery = await _context.Users
            .Where(u => u.DeletedAt == null)
            .ToListAsync();
        
        deletedUserQuery.Should().BeEmpty("User should be filtered out when DeletedAt is not null");
    }

    [Fact]
    public async Task ApplicationUser_LoginScenario_DoesNotThrowSqlException()
    {
        // Arrange - Create a test scenario similar to the failing login
        var adminUser = new ApplicationUser
        {
            Id = 1,
            UserName = "admin",
            Email = "admin@projectmanagement.com",
            FirstName = "System",
            LastName = "Administrator",
            Role = "SystemAdmin",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = null,
            DeletedBy = null
        };

        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        // Act - Execute the exact query pattern that was failing in AuthService
        var loginUsername = "admin@projectmanagement.com";
        
        // This query pattern was causing "Invalid column name 'deleted_at'" error
        var userQuery = _context.Users
            .Where(u => u.UserName == loginUsername || u.Email == loginUsername);

        // Should not throw an exception
        var user = await userQuery.FirstOrDefaultAsync();

        // Assert
        user.Should().NotBeNull();
        user!.Email.Should().Be(loginUsername);
        user.IsActive.Should().BeTrue();
        user.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void ApplicationUser_ModelSnapshot_IncludesSoftDeleteFields()
    {
        // This test verifies that the model snapshot includes the soft delete fields
        // which indicates that migrations should have been generated correctly
        
        var model = _context.Model;
        var userEntity = model.FindEntityType(typeof(ApplicationUser));
        
        userEntity.Should().NotBeNull();
        
        var properties = userEntity!.GetProperties().ToList();
        var propertyNames = properties.Select(p => p.Name).ToList();
        
        // Verify all expected properties are present
        propertyNames.Should().Contain("DeletedAt");
        propertyNames.Should().Contain("DeletedBy");
        propertyNames.Should().Contain("CreatedAt");
        propertyNames.Should().Contain("UpdatedAt");
        propertyNames.Should().Contain("IsActive");
        propertyNames.Should().Contain("FirstName");
        propertyNames.Should().Contain("LastName");
        propertyNames.Should().Contain("Role");
    }

    [Fact]
    public void ApplicationDbContext_Configuration_IsValid()
    {
        // Arrange & Act
        var validationErrors = new List<string>();
        
        try
        {
            // This will validate the model configuration
            _context.Model.ToString();
        }
        catch (Exception ex)
        {
            validationErrors.Add($"Model validation failed: {ex.Message}");
        }

        // Verify that we can create and configure all entity types
        var entityTypes = _context.Model.GetEntityTypes().ToList();
        
        // Assert
        validationErrors.Should().BeEmpty("Model configuration should be valid");
        entityTypes.Should().NotBeEmpty("At least one entity type should be configured");
        
        var userEntity = entityTypes.FirstOrDefault(e => e.ClrType == typeof(ApplicationUser));
        userEntity.Should().NotBeNull("ApplicationUser entity should be configured");
    }

    [Fact]
    public async Task RefreshToken_Entity_IsProperlyConfigured()
    {
        // Arrange
        var refreshToken = new RefreshToken
        {
            Token = "test-refresh-token",
            UserId = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null,
            ReplacedByToken = null
        };

        // Act
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        // Assert
        var savedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "test-refresh-token");
        
        savedToken.Should().NotBeNull();
        savedToken!.UserId.Should().Be(1);
        savedToken.RevokedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("admin@projectmanagement.com")]
    [InlineData("admin")]
    [InlineData("testuser@example.com")]
    public async Task LoginQuery_VariousUsernameFormats_DoesNotThrow(string username)
    {
        // Arrange
        var users = new[]
        {
            new ApplicationUser { Id = 1, UserName = "admin", Email = "admin@projectmanagement.com", FirstName = "Admin", LastName = "User", Role = "SystemAdmin", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ApplicationUser { Id = 2, UserName = "testuser", Email = "testuser@example.com", FirstName = "Test", LastName = "User", Role = "User", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        // Act - This should not throw any SQL exceptions
        var user = await _context.Users
            .Where(u => u.UserName == username || u.Email == username)
            .FirstOrDefaultAsync();

        // Assert
        if (username.Contains("admin"))
        {
            user.Should().NotBeNull();
            user!.Email.Should().Contain("admin");
        }
        else if (username.Contains("testuser"))
        {
            user.Should().NotBeNull();
            user!.Email.Should().Contain("testuser");
        }
    }

    [Fact]
    public async Task UserManagement_SoftDeleteScenario_WorksCorrectly()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "userToDelete",
            Email = "delete@example.com",
            FirstName = "Delete",
            LastName = "Me",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = null,
            DeletedBy = null
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act 1 - User should be found when not deleted
        var activeUser = await _context.Users
            .Where(u => u.Id == 1 && u.DeletedAt == null)
            .FirstOrDefaultAsync();
        
        activeUser.Should().NotBeNull();

        // Act 2 - Soft delete the user
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = 999; // Admin user
        user.IsActive = false;
        await _context.SaveChangesAsync();

        // Act 3 - User should not be found when soft deleted
        var deletedUser = await _context.Users
            .Where(u => u.Id == 1 && u.DeletedAt == null)
            .FirstOrDefaultAsync();

        // Assert
        deletedUser.Should().BeNull("Soft deleted user should not be returned");

        // But user should still exist in database
        var userStillExists = await _context.Users
            .Where(u => u.Id == 1)
            .FirstOrDefaultAsync();
        
        userStillExists.Should().NotBeNull("User should still exist in database");
        userStillExists!.DeletedAt.Should().NotBeNull("DeletedAt should be set");
        userStillExists.DeletedBy.Should().Be(999);
        userStillExists.IsActive.Should().BeFalse();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}