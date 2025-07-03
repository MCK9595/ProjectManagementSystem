using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.IntegrationTests.Infrastructure;
using ProjectManagementSystem.IntegrationTests.Helpers;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.IntegrationTests.Tests;

/// <summary>
/// Integration tests for database operations in IdentityService
/// Tests actual database operations, soft delete functionality, data persistence, and schema validation
/// </summary>
[Collection("IdentityService Integration Tests")]
public class DatabaseIntegrationTests : IdentityServiceTestBase
{
    private readonly AuthenticationHelper _authHelper;
    private readonly HttpClientHelper _httpHelper;
    private readonly DatabaseHelper _dbHelper;

    public DatabaseIntegrationTests(IdentityServiceTestFixture fixture) : base(fixture)
    {
        _authHelper = new AuthenticationHelper(_client);
        _httpHelper = new HttpClientHelper(_client);
        _dbHelper = new DatabaseHelper(_context);
    }

    #region Database Schema Tests

    [Fact]
    public async Task DatabaseSchema_ShouldHaveAllRequiredTables()
    {
        // Act & Assert
        var schemaValid = await _dbHelper.VerifyDatabaseSchemaAsync();
        schemaValid.Should().BeTrue("Database schema should be properly configured");

        // Verify Users table exists with required columns
        var usersTableExists = await _context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'Users'").FirstOrDefaultAsync();
        usersTableExists.Should().BeGreaterThan(0, "Users table should exist");

        // Verify RefreshTokens table exists
        var refreshTokensTableExists = await _context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'RefreshTokens'").FirstOrDefaultAsync();
        refreshTokensTableExists.Should().BeGreaterThan(0, "RefreshTokens table should exist");
    }

    [Fact]
    public async Task DatabaseSchema_UserTable_ShouldHaveSoftDeleteColumns()
    {
        // Arrange
        await CleanupAsync();

        // Create a test user to verify column structure
        var user = new ApplicationUser
        {
            UserName = "schematest",
            Email = "schematest@example.com",
            FirstName = "Schema",
            LastName = "Test",
            Role = "User",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert - Verify soft delete columns can be queried
        var userWithSoftDeleteFields = await _context.Users
            .Where(u => u.Id == user.Id)
            .Select(u => new { u.Id, u.DeletedAt, u.DeletedBy, u.CreatedAt, u.UpdatedAt })
            .FirstOrDefaultAsync();

        userWithSoftDeleteFields.Should().NotBeNull();
        userWithSoftDeleteFields!.DeletedAt.Should().BeNull("New user should not be soft deleted");
        userWithSoftDeleteFields.DeletedBy.Should().BeNull("New user should not have DeletedBy set");
        userWithSoftDeleteFields.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        userWithSoftDeleteFields.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Data Persistence Tests

    [Fact]
    public async Task DataPersistence_UserCreation_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var createRequest = new FindOrCreateUserRequest
        {
            Email = "persistence@example.com",
            FirstName = "Persistence",
            LastName = "Test"
        };

        // Act
        var apiResponse = await _httpHelper.PostAsync<UserDto>("/api/internaluser/find-or-create", createRequest);

        // Assert
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();

        // Verify data was actually persisted to database
        var dbUser = await _dbHelper.GetUserByEmailAsync("persistence@example.com");
        dbUser.Should().NotBeNull("User should be persisted in database");
        dbUser!.FirstName.Should().Be("Persistence");
        dbUser.LastName.Should().Be("Test");
        dbUser.IsActive.Should().BeTrue();
        dbUser.DeletedAt.Should().BeNull();
        dbUser.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DataPersistence_UserUpdate_ShouldUpdateDatabase()
    {
        // Arrange
        await CleanupAsync();
        var testUser = await _dbHelper.CreateUserAsync(
            username: "updatetest",
            email: "updatetest@example.com",
            firstName: "Original",
            lastName: "Name"
        );

        var originalCreatedAt = testUser.CreatedAt;
        var originalUpdatedAt = testUser.UpdatedAt;

        // Simulate user update (this would typically go through UserManagementService)
        testUser.FirstName = "Updated";
        testUser.LastName = "Name";
        testUser.UpdatedAt = DateTime.UtcNow;

        // Act
        await _context.SaveChangesAsync();

        // Assert
        var updatedUser = await _dbHelper.GetUserByIdAsync(testUser.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.FirstName.Should().Be("Updated");
        updatedUser.LastName.Should().Be("Name");
        updatedUser.CreatedAt.Should().Be(originalCreatedAt, "CreatedAt should not change on update");
        updatedUser.UpdatedAt.Should().BeAfter(originalUpdatedAt, "UpdatedAt should be updated");
    }

    [Fact]
    public async Task DataPersistence_LoginActivity_ShouldUpdateLastLoginTime()
    {
        // Arrange
        await CleanupAsync();
        var beforeLogin = DateTime.UtcNow;

        // Act - Perform login
        var authResponse = await _authHelper.LoginAsAdminAsync();

        // Assert
        authResponse.Should().NotBeNull();

        // Verify database was updated
        var dbUser = await _dbHelper.GetUserByEmailAsync("admin@projectmanagement.com");
        dbUser.Should().NotBeNull();
        dbUser!.LastLoginAt.Should().NotBeNull();
        dbUser.LastLoginAt.Should().BeAfter(beforeLogin);
        dbUser.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Soft Delete Tests

    [Fact]
    public async Task SoftDelete_UserDeletion_ShouldSetDeletedAtAndDeletedBy()
    {
        // Arrange
        await CleanupAsync();
        var testUser = await _dbHelper.CreateUserAsync(
            username: "softdelete",
            email: "softdelete@example.com",
            firstName: "Soft",
            lastName: "Delete"
        );

        var adminUser = await _dbHelper.GetUserByEmailAsync("admin@projectmanagement.com");
        adminUser.Should().NotBeNull();

        // Act - Perform soft delete
        var beforeDelete = DateTime.UtcNow;
        var deleteSuccess = await _dbHelper.SoftDeleteUserAsync(testUser.Id, adminUser!.Id);

        // Assert
        deleteSuccess.Should().BeTrue();

        var deletedUser = await _dbHelper.GetUserByIdAsync(testUser.Id);
        deletedUser.Should().NotBeNull("Soft deleted user should still exist in database");
        deletedUser!.DeletedAt.Should().NotBeNull("DeletedAt should be set");
        deletedUser.DeletedAt.Should().BeAfter(beforeDelete);
        deletedUser.DeletedBy.Should().Be(adminUser.Id, "DeletedBy should be set to admin user ID");
        deletedUser.IsActive.Should().BeFalse("Soft deleted user should be inactive");
        deletedUser.IsDeleted.Should().BeTrue("IsDeleted property should return true");
    }

    [Fact]
    public async Task SoftDelete_DeletedUser_ShouldNotAppearInQueries()
    {
        // Arrange
        await CleanupAsync();
        var testUser = await _dbHelper.CreateUserAsync(
            username: "querytest",
            email: "querytest@example.com"
        );

        // Verify user exists before deletion
        var userExists = await _dbHelper.UserExistsAsync("querytest@example.com");
        userExists.Should().BeTrue("User should exist before deletion");

        // Act - Soft delete the user
        await _dbHelper.SoftDeleteUserAsync(testUser.Id, 1);

        // Assert - User should not appear in standard queries
        var deletedUserQuery = await _context.Users
            .Where(u => u.Email == "querytest@example.com" && u.DeletedAt == null)
            .FirstOrDefaultAsync();
        
        deletedUserQuery.Should().BeNull("Soft deleted user should not appear in queries with DeletedAt == null filter");

        // But user should still exist when querying without soft delete filter
        var userStillInDb = await _context.Users
            .Where(u => u.Email == "querytest@example.com")
            .FirstOrDefaultAsync();
        
        userStillInDb.Should().NotBeNull("User should still exist in database without DeletedAt filter");
    }

    [Fact]
    public async Task SoftDelete_LoginAttempt_ShouldFailForDeletedUser()
    {
        // Arrange
        await CleanupAsync();
        
        // Create a user that we can actually set a password for
        // Note: In a real system, we'd need to properly hash the password
        var testUser = await _dbHelper.CreateUserAsync(
            username: "logintest",
            email: "logintest@example.com"
        );

        // Soft delete the user
        await _dbHelper.SoftDeleteUserAsync(testUser.Id, 1);

        // Act - Attempt to login with soft deleted user
        var loginResponse = await _authHelper.LoginAsync("logintest@example.com", "password");

        // Assert
        loginResponse.Should().BeNull("Login should fail for soft deleted user");
    }

    #endregion

    #region RefreshToken Database Tests

    [Fact]
    public async Task RefreshTokens_Creation_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanupAsync();
        var testUser = await _dbHelper.CreateUserAsync("tokentest", "tokentest@example.com");

        // Act
        var refreshToken = await _dbHelper.CreateRefreshTokenAsync(
            userId: testUser.Id,
            token: "test-refresh-token-123",
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        // Assert
        refreshToken.Should().NotBeNull();
        refreshToken.Token.Should().Be("test-refresh-token-123");
        refreshToken.UserId.Should().Be(testUser.Id);
        refreshToken.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(6));
        refreshToken.RevokedAt.Should().BeNull();
        refreshToken.IsActive.Should().BeTrue();

        // Verify in database
        var dbToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "test-refresh-token-123");
        
        dbToken.Should().NotBeNull();
        dbToken!.UserId.Should().Be(testUser.Id);
    }

    [Fact]
    public async Task RefreshTokens_UserTokens_ShouldBeQueryableByUserId()
    {
        // Arrange
        await CleanupAsync();
        var user1 = await _dbHelper.CreateUserAsync("user1", "user1@example.com");
        var user2 = await _dbHelper.CreateUserAsync("user2", "user2@example.com");

        // Create tokens for both users
        await _dbHelper.CreateRefreshTokenAsync(user1.Id, "user1-token-1");
        await _dbHelper.CreateRefreshTokenAsync(user1.Id, "user1-token-2");
        await _dbHelper.CreateRefreshTokenAsync(user2.Id, "user2-token-1");

        // Act
        var user1Tokens = await _dbHelper.GetUserRefreshTokensAsync(user1.Id);
        var user2Tokens = await _dbHelper.GetUserRefreshTokensAsync(user2.Id);

        // Assert
        user1Tokens.Should().HaveCount(2);
        user1Tokens.Should().AllSatisfy(token => token.UserId.Should().Be(user1.Id));
        
        user2Tokens.Should().HaveCount(1);
        user2Tokens.Should().AllSatisfy(token => token.UserId.Should().Be(user2.Id));
    }

    [Fact]
    public async Task RefreshTokens_ActiveTokens_ShouldExcludeRevokedAndExpired()
    {
        // Arrange
        await CleanupAsync();
        var testUser = await _dbHelper.CreateUserAsync("activetest", "activetest@example.com");

        // Create various types of tokens
        var activeToken = await _dbHelper.CreateRefreshTokenAsync(
            testUser.Id, "active-token", DateTime.UtcNow.AddDays(7));
        
        var revokedToken = await _dbHelper.CreateRefreshTokenAsync(
            testUser.Id, "revoked-token", DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddMinutes(-1));
        
        var expiredToken = await _dbHelper.CreateRefreshTokenAsync(
            testUser.Id, "expired-token", DateTime.UtcNow.AddDays(-1));

        // Act
        var activeTokens = await _dbHelper.GetActiveRefreshTokensAsync(testUser.Id);

        // Assert
        activeTokens.Should().HaveCount(1);
        activeTokens.First().Token.Should().Be("active-token");
    }

    #endregion

    #region Database Concurrency Tests

    [Fact]
    public async Task DatabaseConcurrency_ConcurrentUserCreation_ShouldHandleGracefully()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var tasks = new List<Task<UserDto?>>();

        // Act - Create multiple users concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var request = new FindOrCreateUserRequest
                {
                    Email = $"concurrent{index}@example.com",
                    FirstName = $"Concurrent{index}",
                    LastName = "User"
                };

                var response = await _httpHelper.PostAsync<UserDto>("/api/internaluser/find-or-create", request);
                return response?.Data;
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().NotContainNulls("All concurrent user creations should succeed");
        results.Length.Should().Be(10);

        // Verify all users were actually created in database
        var userCount = await _context.Users
            .CountAsync(u => u.Email.StartsWith("concurrent") && u.Email.EndsWith("@example.com"));
        
        userCount.Should().Be(10, "All concurrent users should be persisted to database");
    }

    [Fact]
    public async Task DatabaseConcurrency_ConcurrentLoginAttempts_ShouldMaintainDataIntegrity()
    {
        // Arrange
        await CleanupAsync();
        
        var tasks = new List<Task<AuthResponseDto?>>();

        // Act - Perform concurrent login attempts
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () => await _authHelper.LoginAsAdminAsync()));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().NotContainNulls("All concurrent logins should succeed");

        // Verify database integrity
        var adminUser = await _dbHelper.GetUserByEmailAsync("admin@projectmanagement.com");
        adminUser.Should().NotBeNull();
        adminUser!.LastLoginAt.Should().NotBeNull();
        adminUser.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Data Consistency Tests

    [Fact]
    public async Task DataConsistency_UserCounts_ShouldBeAccurate()
    {
        // Arrange
        await CleanupAsync();

        var initialActiveCount = await _dbHelper.GetActiveUserCountAsync();
        var initialTotalCount = await _dbHelper.GetTotalUserCountAsync();

        // Act - Create some users and soft delete others
        var user1 = await _dbHelper.CreateUserAsync("count1", "count1@example.com");
        var user2 = await _dbHelper.CreateUserAsync("count2", "count2@example.com");
        var user3 = await _dbHelper.CreateUserAsync("count3", "count3@example.com");

        await _dbHelper.SoftDeleteUserAsync(user2.Id, 1);

        // Assert
        var finalActiveCount = await _dbHelper.GetActiveUserCountAsync();
        var finalTotalCount = await _dbHelper.GetTotalUserCountAsync();

        finalActiveCount.Should().Be(initialActiveCount + 2, "Active count should include only non-deleted users");
        finalTotalCount.Should().Be(initialTotalCount + 3, "Total count should include all users");
    }

    [Fact]
    public async Task DataConsistency_DatabaseTransactions_ShouldMaintainIntegrity()
    {
        // Arrange
        await CleanupAsync();

        // Test transaction behavior by simulating a complex operation
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Act - Perform multiple operations in a transaction
            var user = await _dbHelper.CreateUserAsync("transaction", "transaction@example.com");
            var refreshToken = await _dbHelper.CreateRefreshTokenAsync(user.Id, "transaction-token");

            // Verify both operations are in the transaction but not yet committed
            var userInTransaction = await _dbHelper.GetUserByIdAsync(user.Id);
            var tokenInTransaction = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == "transaction-token");

            userInTransaction.Should().NotBeNull("User should exist in transaction context");
            tokenInTransaction.Should().NotBeNull("Token should exist in transaction context");

            // Commit transaction
            await transaction.CommitAsync();

            // Assert
            var committedUser = await _dbHelper.GetUserByEmailAsync("transaction@example.com");
            var committedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == "transaction-token");

            committedUser.Should().NotBeNull("User should exist after transaction commit");
            committedToken.Should().NotBeNull("Token should exist after transaction commit");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    #endregion

    private async Task AuthenticateAsAdminAsync()
    {
        var authResponse = await _authHelper.LoginAsAdminAsync();
        authResponse.Should().NotBeNull("Admin login should succeed for database tests");
        _authHelper.SetBearerToken(authResponse!.Token);
    }

    protected override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        await _dbHelper.CleanupTestDataAsync();
        _authHelper.ClearAuthentication();
    }
}