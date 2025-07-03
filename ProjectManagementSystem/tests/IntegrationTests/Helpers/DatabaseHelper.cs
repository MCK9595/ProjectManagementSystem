using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;

namespace ProjectManagementSystem.IntegrationTests.Helpers;

/// <summary>
/// Helper class for database operations in integration tests
/// </summary>
public class DatabaseHelper
{
    private readonly ApplicationDbContext _context;

    public DatabaseHelper(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Create a test user with specified properties
    /// </summary>
    public async Task<ApplicationUser> CreateUserAsync(
        string username,
        string email,
        string firstName = "Test",
        string lastName = "User",
        string role = "User",
        bool isActive = true,
        bool emailConfirmed = true,
        DateTime? deletedAt = null,
        int? deletedBy = null)
    {
        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            IsActive = isActive,
            EmailConfirmed = emailConfirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        return user;
    }

    /// <summary>
    /// Create a refresh token for a user
    /// </summary>
    public async Task<RefreshToken> CreateRefreshTokenAsync(
        int userId,
        string token = null!,
        DateTime? expiresAt = null,
        DateTime? revokedAt = null,
        string? replacedByToken = null)
    {
        var refreshToken = new RefreshToken
        {
            Token = token ?? Guid.NewGuid().ToString("N"),
            UserId = userId,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = revokedAt,
            ReplacedByToken = replacedByToken
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
        
        return refreshToken;
    }

    /// <summary>
    /// Get user by email
    /// </summary>
    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Get user by username
    /// </summary>
    public async Task<ApplicationUser?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.UserName == username);
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    public async Task<ApplicationUser?> GetUserByIdAsync(int id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Get all refresh tokens for a user
    /// </summary>
    public async Task<List<RefreshToken>> GetUserRefreshTokensAsync(int userId)
    {
        return await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();
    }

    /// <summary>
    /// Get active refresh tokens for a user
    /// </summary>
    public async Task<List<RefreshToken>> GetActiveRefreshTokensAsync(int userId)
    {
        return await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
    }

    /// <summary>
    /// Soft delete a user
    /// </summary>
    public async Task<bool> SoftDeleteUserAsync(int userId, int deletedByUserId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null) return false;

        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = deletedByUserId;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Update user's last login time
    /// </summary>
    public async Task UpdateLastLoginAsync(int userId, DateTime lastLoginAt)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = lastLoginAt;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Update user's failed login attempts
    /// </summary>
    public async Task UpdateFailedLoginAttemptsAsync(int userId, int attempts)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.FailedLoginAttempts = attempts;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Lock user account until specified time
    /// </summary>
    public async Task LockUserAsync(int userId, DateTime lockedUntil)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.LockedUntil = lockedUntil;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Unlock user account
    /// </summary>
    public async Task UnlockUserAsync(int userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Check if user exists (not soft deleted)
    /// </summary>
    public async Task<bool> UserExistsAsync(string email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email == email && u.DeletedAt == null);
    }

    /// <summary>
    /// Get count of active users
    /// </summary>
    public async Task<int> GetActiveUserCountAsync()
    {
        return await _context.Users
            .CountAsync(u => u.IsActive && u.DeletedAt == null);
    }

    /// <summary>
    /// Get count of all users (including soft deleted)
    /// </summary>
    public async Task<int> GetTotalUserCountAsync()
    {
        return await _context.Users.CountAsync();
    }

    /// <summary>
    /// Clean up test data
    /// </summary>
    public async Task CleanupTestDataAsync()
    {
        // Remove test refresh tokens
        var testTokens = await _context.RefreshTokens
            .Where(rt => rt.Token.Contains("test") || rt.Token.Length == 32)
            .ToListAsync();
        _context.RefreshTokens.RemoveRange(testTokens);

        // Remove test users (except admin)
        var testUsers = await _context.Users
            .Where(u => u.Email.Contains("test") && u.Email != "admin@projectmanagement.com")
            .ToListAsync();
        _context.Users.RemoveRange(testUsers);

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Verify database schema has all required tables and columns
    /// </summary>
    public async Task<bool> VerifyDatabaseSchemaAsync()
    {
        try
        {
            // Check if Users table has all required columns
            var userWithSoftDelete = await _context.Users
                .Select(u => new { u.Id, u.DeletedAt, u.DeletedBy })
                .FirstOrDefaultAsync();

            // Check if RefreshTokens table exists and has required columns
            var refreshTokenCheck = await _context.RefreshTokens
                .Select(rt => new { rt.Id, rt.Token, rt.UserId, rt.ExpiresAt, rt.RevokedAt })
                .FirstOrDefaultAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }
}