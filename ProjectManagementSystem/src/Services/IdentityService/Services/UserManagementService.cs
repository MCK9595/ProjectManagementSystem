using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.IdentityService.Services;

public class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UserManagementService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<PagedResult<UserListDto>> GetUsersAsync(UserSearchRequest request)
    {
        var query = _context.Users.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(u => 
                u.UserName!.ToLower().Contains(searchTerm) ||
                u.Email!.ToLower().Contains(searchTerm) ||
                u.FirstName.ToLower().Contains(searchTerm) ||
                u.LastName.ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(u => u.Role == request.Role);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        // Apply sorting
        query = request.SortBy?.ToLower() switch
        {
            "username" => request.SortDirection?.ToLower() == "desc" 
                ? query.OrderByDescending(u => u.UserName)
                : query.OrderBy(u => u.UserName),
            "email" => request.SortDirection?.ToLower() == "desc"
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email),
            "firstname" => request.SortDirection?.ToLower() == "desc"
                ? query.OrderByDescending(u => u.FirstName)
                : query.OrderBy(u => u.FirstName),
            "lastname" => request.SortDirection?.ToLower() == "desc"
                ? query.OrderByDescending(u => u.LastName)
                : query.OrderBy(u => u.LastName),
            "role" => request.SortDirection?.ToLower() == "desc"
                ? query.OrderByDescending(u => u.Role)
                : query.OrderBy(u => u.Role),
            "lastlogin" => request.SortDirection?.ToLower() == "desc"
                ? query.OrderByDescending(u => u.LastLoginAt)
                : query.OrderBy(u => u.LastLoginAt),
            _ => request.SortDirection?.ToLower() == "desc"
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var users = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Username = u.UserName!,
                Email = u.Email!,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();

        return new PagedResult<UserListDto>(users, totalCount, request.PageNumber, request.PageSize);
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }

        return new UserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            // Check if user already exists
            if (await UserExistsAsync(request.Username, request.Email))
            {
                _logger.LogWarning("User creation failed: Username '{Username}' or email '{Email}' already exists", 
                    request.Username, request.Email);
                return null;
            }

            // Validate role
            if (!await _roleManager.RoleExistsAsync(request.Role))
            {
                _logger.LogWarning("User creation failed: Invalid role '{Role}'", request.Role);
                return null;
            }

            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = request.Role,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                _logger.LogWarning("User creation failed for username '{Username}': {Errors}", 
                    request.Username, string.Join(", ", result.Errors.Select(e => e.Description)));
                return null;
            }

            // Add user to role
            await _userManager.AddToRoleAsync(user, request.Role);

            _logger.LogInformation("User '{Username}' created successfully with role '{Role}'", 
                user.UserName, request.Role);

            return await GetUserByIdAsync(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user with username '{Username}'", request.Username);
            return null;
        }
    }

    public async Task<UserDto?> UpdateUserAsync(int userId, UpdateUserRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Update failed: User with ID {UserId} not found", userId);
                return null;
            }

            var hasChanges = false;

            // Update basic information
            if (!string.IsNullOrWhiteSpace(request.FirstName) && request.FirstName != user.FirstName)
            {
                user.FirstName = request.FirstName;
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(request.LastName) && request.LastName != user.LastName)
            {
                user.LastName = request.LastName;
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
            {
                // Check if email is already taken by another user
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null && existingUser.Id != userId)
                {
                    _logger.LogWarning("Update failed: Email '{Email}' is already taken", request.Email);
                    return null;
                }

                user.Email = request.Email;
                hasChanges = true;
            }

            // Update role if specified
            if (!string.IsNullOrWhiteSpace(request.Role) && request.Role != user.Role)
            {
                if (!await _roleManager.RoleExistsAsync(request.Role))
                {
                    _logger.LogWarning("Update failed: Invalid role '{Role}'", request.Role);
                    return null;
                }

                // Remove from current role and add to new role
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }

                await _userManager.AddToRoleAsync(user, request.Role);
                user.Role = request.Role;
                hasChanges = true;
            }

            // Update status if specified
            if (request.IsActive.HasValue && request.IsActive.Value != user.IsActive)
            {
                user.IsActive = request.IsActive.Value;
                hasChanges = true;
            }

            if (hasChanges)
            {
                user.UpdatedAt = DateTime.UtcNow;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("User update failed for ID {UserId}: {Errors}", 
                        userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return null;
                }

                _logger.LogInformation("User '{Username}' (ID: {UserId}) updated successfully", 
                    user.UserName, userId);
            }

            return await GetUserByIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with ID {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Delete failed: User with ID {UserId} not found", userId);
                return false;
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogWarning("User deletion failed for ID {UserId}: {Errors}", 
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }

            _logger.LogInformation("User '{Username}' (ID: {UserId}) deleted successfully", 
                user.UserName, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user with ID {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ChangeUserRoleAsync(int userId, string role)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Role change failed: User with ID {UserId} not found", userId);
                return false;
            }

            if (!await _roleManager.RoleExistsAsync(role))
            {
                _logger.LogWarning("Role change failed: Invalid role '{Role}'", role);
                return false;
            }

            // Remove from current roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            // Add to new role
            await _userManager.AddToRoleAsync(user, role);
            
            // Update user role property
            user.Role = role;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User '{Username}' (ID: {UserId}) role changed to '{Role}'", 
                user.UserName, userId, role);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing role for user with ID {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ChangeUserStatusAsync(int userId, bool isActive)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Status change failed: User with ID {UserId} not found", userId);
                return false;
            }

            user.IsActive = isActive;
            user.UpdatedAt = DateTime.UtcNow;
            
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Status change failed for user ID {UserId}: {Errors}", 
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }

            _logger.LogInformation("User '{Username}' (ID: {UserId}) status changed to {Status}", 
                user.UserName, userId, isActive ? "Active" : "Inactive");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing status for user with ID {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> UserExistsAsync(string username, string email, int? excludeUserId = null)
    {
        var query = _context.Users.Where(u => 
            u.UserName!.ToLower() == username.ToLower() || 
            u.Email!.ToLower() == email.ToLower());

        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludeUserId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<List<string>> GetAvailableRolesAsync()
    {
        // Return only global roles that can be assigned during user management
        var globalRoles = Roles.GlobalRoles.ToList();
        
        // Verify these roles exist in the database
        var existingRoles = await _roleManager.Roles
            .Where(r => globalRoles.Contains(r.Name!))
            .Select(r => r.Name!)
            .ToListAsync();
            
        return existingRoles;
    }

    public async Task<int> GetTotalUsersCountAsync()
    {
        return await _context.Users.CountAsync();
    }

    public async Task<bool> CanDeleteUserAsync(int userId, int currentUserId)
    {
        // Prevent self-deletion
        if (userId == currentUserId)
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        // Add additional business rules as needed
        // For example, prevent deletion of the last SystemAdmin
        if (user.Role == Roles.SystemAdmin)
        {
            var systemAdminCount = await _context.Users
                .Where(u => u.Role == Roles.SystemAdmin && u.IsActive)
                .CountAsync();
            
            return systemAdminCount > 1;
        }

        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Password change failed: User with ID {UserId} not found", userId);
                return false;
            }

            // Generate new password hash
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Password change failed for user ID {UserId}: {Errors}", 
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }

            _logger.LogInformation("Password changed successfully for user '{Username}' (ID: {UserId})", 
                user.UserName, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user with ID {UserId}", userId);
            return false;
        }
    }
}