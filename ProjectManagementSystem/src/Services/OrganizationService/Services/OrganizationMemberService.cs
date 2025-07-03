using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.OrganizationService.Data;
using ProjectManagementSystem.OrganizationService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.OrganizationService.Services;

public class OrganizationMemberService : IOrganizationMemberService
{
    private readonly OrganizationDbContext _context;
    private readonly ILogger<OrganizationMemberService> _logger;
    private readonly IUserService _userService;

    public OrganizationMemberService(OrganizationDbContext context, ILogger<OrganizationMemberService> logger, IUserService userService)
    {
        _context = context;
        _logger = logger;
        _userService = userService;
    }

    public async Task<PagedResult<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, int requestingUserId, int page, int pageSize)
    {
        // Check if requesting user can access organization
        var canAccess = await _context.OrganizationUsers
            .AnyAsync(ou => ou.OrganizationId == organizationId && 
                           ou.UserId == requestingUserId && 
                           ou.IsActive);

        if (!canAccess)
        {
            return new PagedResult<OrganizationMemberDto>
            {
                Items = new List<OrganizationMemberDto>(),
                TotalCount = 0,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        var query = _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId && ou.IsActive)
            .OrderBy(ou => ou.JoinedAt);

        var total = await query.CountAsync();
        var members = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ou => new OrganizationMemberDto
            {
                Id = ou.Id,
                OrganizationId = ou.OrganizationId,
                UserId = ou.UserId,
                Role = ou.Role,
                JoinedAt = ou.JoinedAt
            })
            .ToListAsync();

        // Fetch user details from IdentityService
        if (members.Any())
        {
            _logger.LogInformation("=== ORGANIZATION MEMBER SERVICE - Fetching user details for {Count} organization members ===", members.Count);
            var userIds = members.Select(m => m.UserId).ToList();
            _logger.LogInformation("Member user IDs: [{UserIds}]", string.Join(", ", userIds));
            
            var users = await _userService.GetUsersByIdsAsync(userIds);
            _logger.LogInformation("UserService returned {UserCount} users", users.Count);

            // Map users to members
            foreach (var member in members)
            {
                member.User = users.FirstOrDefault(u => u.Id == member.UserId);
                if (member.User == null)
                {
                    _logger.LogWarning("Could not find user details for user ID: {UserId} in organization {OrganizationId}", 
                        member.UserId, organizationId);
                }
                else
                {
                    _logger.LogInformation("Successfully mapped user {UserId} ({Username}) to member {MemberId}", 
                        member.UserId, member.User.Username, member.Id);
                }
            }
            
            _logger.LogInformation("=== ORGANIZATION MEMBER SERVICE - User mapping completed ===");
        }
        else
        {
            _logger.LogInformation("No members found, skipping user details fetch");
        }

        return new PagedResult<OrganizationMemberDto>
        {
            Items = members,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<OrganizationMemberDto?> AddMemberAsync(Guid organizationId, AddMemberDto addMemberDto, int requestingUserId)
    {
        // Check if requesting user is admin or owner
        var requestingUserRole = await _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId && 
                        ou.UserId == requestingUserId && 
                        ou.IsActive)
            .Select(ou => ou.Role)
            .FirstOrDefaultAsync();

        if (requestingUserRole != Roles.OrganizationOwner && requestingUserRole != Roles.OrganizationAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to add member without proper permissions", requestingUserId);
            return null;
        }

        // Check if user is already a member
        var existingMembership = await _context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == addMemberDto.UserId);

        if (existingMembership != null)
        {
            if (existingMembership.IsActive)
            {
                _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", 
                    addMemberDto.UserId, organizationId);
                return null;
            }
            else
            {
                // Reactivate the membership
                existingMembership.IsActive = true;
                existingMembership.Role = addMemberDto.Role;
                existingMembership.JoinedAt = DateTime.UtcNow;
            }
        }
        else
        {
            // Create new membership
            existingMembership = new OrganizationUser
            {
                OrganizationId = organizationId,
                UserId = addMemberDto.UserId,
                Role = addMemberDto.Role,
                JoinedAt = DateTime.UtcNow
            };

            _context.OrganizationUsers.Add(existingMembership);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} added to organization {OrganizationId} with role {Role}", 
            addMemberDto.UserId, organizationId, addMemberDto.Role);

        var memberDto = new OrganizationMemberDto
        {
            Id = existingMembership.Id,
            OrganizationId = existingMembership.OrganizationId,
            UserId = existingMembership.UserId,
            Role = existingMembership.Role,
            JoinedAt = existingMembership.JoinedAt
        };

        // Fetch user details for the newly added member
        memberDto.User = await _userService.GetUserByIdAsync(addMemberDto.UserId);
        if (memberDto.User == null)
        {
            _logger.LogWarning("Could not fetch user details for newly added member {UserId}", addMemberDto.UserId);
        }

        return memberDto;
    }

    public async Task<OrganizationMemberDto?> AddMemberByEmailAsync(Guid organizationId, AddMemberByEmailDto addMemberDto, int requestingUserId)
    {
        // Check if requesting user is admin or owner
        var requestingUserRole = await _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId && 
                        ou.UserId == requestingUserId && 
                        ou.IsActive)
            .Select(ou => ou.Role)
            .FirstOrDefaultAsync();

        if (requestingUserRole != Roles.OrganizationOwner && requestingUserRole != Roles.OrganizationAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to add member by email without proper permissions", requestingUserId);
            return null;
        }

        try
        {
            // Call IdentityService to find or create user
            _logger.LogInformation("Calling IdentityService to find or create user with email {Email}", addMemberDto.Email);
            var user = await _userService.FindOrCreateUserAsync(addMemberDto.Email, addMemberDto.FirstName, addMemberDto.LastName);
            
            if (user == null)
            {
                _logger.LogWarning("Failed to find or create user with email {Email}", addMemberDto.Email);
                return null;
            }

            _logger.LogInformation("Successfully found/created user {UserId} for email {Email}", user.Id, addMemberDto.Email);

            // Check if user is already a member
            var existingMembership = await _context.OrganizationUsers
                .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == user.Id);

            if (existingMembership != null)
            {
                if (existingMembership.IsActive)
                {
                    _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", 
                        user.Id, organizationId);
                    return null;
                }
                else
                {
                    // Reactivate the membership
                    existingMembership.IsActive = true;
                    existingMembership.Role = addMemberDto.OrganizationRole;
                    existingMembership.JoinedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Create new membership
                existingMembership = new OrganizationUser
                {
                    OrganizationId = organizationId,
                    UserId = user.Id,
                    Role = addMemberDto.OrganizationRole,
                    JoinedAt = DateTime.UtcNow
                };

                _context.OrganizationUsers.Add(existingMembership);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} added to organization {OrganizationId} with role {Role}", 
                user.Id, organizationId, addMemberDto.OrganizationRole);

            var memberDto = new OrganizationMemberDto
            {
                Id = existingMembership.Id,
                OrganizationId = existingMembership.OrganizationId,
                UserId = existingMembership.UserId,
                Role = existingMembership.Role,
                JoinedAt = existingMembership.JoinedAt,
                User = user // We already have the user info from the IdentityService call
            };

            return memberDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member by email {Email} to organization {OrganizationId}", 
                addMemberDto.Email, organizationId);
            return null;
        }
    }

    public async Task<OrganizationMemberDto?> AddExistingUserByEmailAsync(Guid organizationId, FindUserByEmailDto findUserDto, int requestingUserId)
    {
        // Check if requesting user is admin or owner
        var requestingUserRole = await _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId && 
                        ou.UserId == requestingUserId && 
                        ou.IsActive)
            .Select(ou => ou.Role)
            .FirstOrDefaultAsync();

        if (requestingUserRole != Roles.OrganizationOwner && requestingUserRole != Roles.OrganizationAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to add existing user by email without proper permissions", requestingUserId);
            return null;
        }

        try
        {
            // Check if user exists in IdentityService
            _logger.LogInformation("Checking if user exists with email {Email}", findUserDto.Email);
            var existingUser = await _userService.CheckUserExistsByEmailAsync(findUserDto.Email);
            
            if (existingUser == null)
            {
                _logger.LogWarning("User with email {Email} does not exist", findUserDto.Email);
                return null;
            }

            _logger.LogInformation("Found existing user {UserId} for email {Email}", existingUser.Id, findUserDto.Email);

            // Check if user is already a member
            var existingMembership = await _context.OrganizationUsers
                .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == existingUser.Id);

            if (existingMembership != null)
            {
                if (existingMembership.IsActive)
                {
                    _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", 
                        existingUser.Id, organizationId);
                    return null;
                }
                else
                {
                    // Reactivate the membership
                    existingMembership.IsActive = true;
                    existingMembership.Role = findUserDto.OrganizationRole;
                    existingMembership.JoinedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Create new membership
                existingMembership = new OrganizationUser
                {
                    OrganizationId = organizationId,
                    UserId = existingUser.Id,
                    Role = findUserDto.OrganizationRole,
                    JoinedAt = DateTime.UtcNow
                };

                _context.OrganizationUsers.Add(existingMembership);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Existing user {UserId} added to organization {OrganizationId} with role {Role}", 
                existingUser.Id, organizationId, findUserDto.OrganizationRole);

            var memberDto = new OrganizationMemberDto
            {
                Id = existingMembership.Id,
                OrganizationId = existingMembership.OrganizationId,
                UserId = existingMembership.UserId,
                Role = existingMembership.Role,
                JoinedAt = existingMembership.JoinedAt,
                User = existingUser
            };

            return memberDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding existing user by email {Email} to organization {OrganizationId}", 
                findUserDto.Email, organizationId);
            return null;
        }
    }

    public async Task<OrganizationMemberDto?> CreateUserAndAddMemberAsync(Guid organizationId, CreateUserAndAddMemberDto createUserDto, int requestingUserId)
    {
        // Check if requesting user is admin or owner
        var requestingUserRole = await _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId && 
                        ou.UserId == requestingUserId && 
                        ou.IsActive)
            .Select(ou => ou.Role)
            .FirstOrDefaultAsync();

        if (requestingUserRole != Roles.OrganizationOwner && requestingUserRole != Roles.OrganizationAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to create and add user without proper permissions", requestingUserId);
            return null;
        }

        // Validate password confirmation
        if (createUserDto.Password != createUserDto.ConfirmPassword)
        {
            _logger.LogWarning("Password confirmation does not match for user creation with email {Email}", createUserDto.Email);
            return null;
        }

        try
        {
            // Create user with specified password
            _logger.LogInformation("Creating new user with password and adding to organization with email {Email}", createUserDto.Email);
            
            var createUserRequest = new CreateUserWithPasswordRequest
            {
                Email = createUserDto.Email,
                FirstName = createUserDto.FirstName,
                LastName = createUserDto.LastName,
                Password = createUserDto.Password,
                ConfirmPassword = createUserDto.ConfirmPassword
            };
            
            var newUser = await _userService.CreateUserWithPasswordAsync(createUserRequest);
            
            if (newUser == null)
            {
                _logger.LogWarning("Failed to create user with email {Email}", createUserDto.Email);
                return null;
            }

            _logger.LogInformation("Successfully created user {UserId} for email {Email}", newUser.Id, createUserDto.Email);

            // Check if user is already a member (shouldn't happen in create scenario, but safety check)
            var existingMembership = await _context.OrganizationUsers
                .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == newUser.Id);

            if (existingMembership != null && existingMembership.IsActive)
            {
                _logger.LogWarning("Newly created user {UserId} is somehow already a member of organization {OrganizationId}", 
                    newUser.Id, organizationId);
                return null;
            }

            if (existingMembership != null)
            {
                // Reactivate the membership
                existingMembership.IsActive = true;
                existingMembership.Role = createUserDto.OrganizationRole;
                existingMembership.JoinedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new membership
                existingMembership = new OrganizationUser
                {
                    OrganizationId = organizationId,
                    UserId = newUser.Id,
                    Role = createUserDto.OrganizationRole,
                    JoinedAt = DateTime.UtcNow
                };

                _context.OrganizationUsers.Add(existingMembership);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("New user {UserId} created and added to organization {OrganizationId} with role {Role}", 
                newUser.Id, organizationId, createUserDto.OrganizationRole);

            var memberDto = new OrganizationMemberDto
            {
                Id = existingMembership.Id,
                OrganizationId = existingMembership.OrganizationId,
                UserId = existingMembership.UserId,
                Role = existingMembership.Role,
                JoinedAt = existingMembership.JoinedAt,
                User = newUser
            };

            return memberDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user and adding to organization {OrganizationId} with email {Email}", 
                organizationId, createUserDto.Email);
            return null;
        }
    }

    public async Task<bool> RemoveMemberAsync(Guid organizationId, int userId, int requestingUserId)
    {
        // Check if requesting user is admin or owner
        var requestingUserRole = await _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId && 
                        ou.UserId == requestingUserId && 
                        ou.IsActive)
            .Select(ou => ou.Role)
            .FirstOrDefaultAsync();

        if (requestingUserRole != Roles.OrganizationOwner && requestingUserRole != Roles.OrganizationAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to remove member without proper permissions", requestingUserId);
            return false;
        }

        // Don't allow removing the owner
        var targetMembership = await _context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && 
                                      ou.UserId == userId && 
                                      ou.IsActive);

        if (targetMembership == null)
            return false;

        if (targetMembership.Role == Roles.OrganizationOwner)
        {
            _logger.LogWarning("Attempted to remove organization owner {UserId} from organization {OrganizationId}", 
                userId, organizationId);
            return false;
        }

        targetMembership.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} removed from organization {OrganizationId}", userId, organizationId);
        
        return true;
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid organizationId, int userId, string newRole, int requestingUserId)
    {
        // Check if requesting user is admin or owner
        var requestingUserRole = await _context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId && 
                        ou.UserId == requestingUserId && 
                        ou.IsActive)
            .Select(ou => ou.Role)
            .FirstOrDefaultAsync();

        if (requestingUserRole != Roles.OrganizationOwner && requestingUserRole != Roles.OrganizationAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to update member role without proper permissions", requestingUserId);
            return false;
        }

        // Don't allow changing the owner's role unless done by the owner themselves
        var targetMembership = await _context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && 
                                      ou.UserId == userId && 
                                      ou.IsActive);

        if (targetMembership == null)
            return false;

        if (targetMembership.Role == Roles.OrganizationOwner && requestingUserId != userId)
        {
            _logger.LogWarning("Attempted to change owner role by non-owner user {UserId}", requestingUserId);
            return false;
        }

        // Validate new role
        if (!Roles.OrganizationRoles.Contains(newRole))
        {
            _logger.LogWarning("Invalid role {Role} specified for user {UserId}", newRole, userId);
            return false;
        }

        targetMembership.Role = newRole;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} role updated to {Role} in organization {OrganizationId}", 
            userId, newRole, organizationId);
        
        return true;
    }
}