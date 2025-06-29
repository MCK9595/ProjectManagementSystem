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

    public OrganizationMemberService(OrganizationDbContext context, ILogger<OrganizationMemberService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<OrganizationMemberDto>> GetMembersAsync(int organizationId, int requestingUserId, int page, int pageSize)
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

        return new PagedResult<OrganizationMemberDto>
        {
            Items = members,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<OrganizationMemberDto?> AddMemberAsync(int organizationId, AddMemberDto addMemberDto, int requestingUserId)
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

        return new OrganizationMemberDto
        {
            Id = existingMembership.Id,
            OrganizationId = existingMembership.OrganizationId,
            UserId = existingMembership.UserId,
            Role = existingMembership.Role,
            JoinedAt = existingMembership.JoinedAt
        };
    }

    public async Task<bool> RemoveMemberAsync(int organizationId, int userId, int requestingUserId)
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

    public async Task<bool> UpdateMemberRoleAsync(int organizationId, int userId, string newRole, int requestingUserId)
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