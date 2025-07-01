using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.OrganizationService.Data;
using ProjectManagementSystem.OrganizationService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.OrganizationService.Services;

public class OrganizationService : IOrganizationService
{
    private readonly OrganizationDbContext _context;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(OrganizationDbContext context, ILogger<OrganizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<OrganizationDto>> GetOrganizationsAsync(int userId, int page, int pageSize)
    {
        var query = _context.Organizations
            .Where(o => o.IsActive && o.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderBy(o => o.Name);

        var total = await query.CountAsync();
        var organizations = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                CreatedAt = o.CreatedAt,
                IsActive = o.IsActive,
                CreatedByUserId = o.CreatedByUserId
            })
            .ToListAsync();

        return new PagedResult<OrganizationDto>
        {
            Items = organizations,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid id, int userId)
    {
        var organization = await _context.Organizations
            .Where(o => o.Id == id && o.IsActive && 
                       o.Members.Any(m => m.UserId == userId && m.IsActive))
            .Select(o => new OrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                CreatedAt = o.CreatedAt,
                IsActive = o.IsActive,
                CreatedByUserId = o.CreatedByUserId
            })
            .FirstOrDefaultAsync();

        return organization;
    }

    public async Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationDto createDto, int createdByUserId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // 重複チェック
                var existingOrg = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Name.ToLower() == createDto.Name.ToLower() && o.IsActive);
                
                if (existingOrg != null)
                {
                    throw new InvalidOperationException($"An organization with the name '{createDto.Name}' already exists.");
                }

                var organization = new Organization
                {
                    Name = createDto.Name,
                    Description = createDto.Description,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Organizations.Add(organization);
                await _context.SaveChangesAsync();

                // Add the creator as an owner
                var ownerMembership = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = createdByUserId,
                    Role = Roles.OrganizationOwner,
                    JoinedAt = DateTime.UtcNow
                };

                _context.OrganizationUsers.Add(ownerMembership);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Organization {OrganizationName} created by user {UserId}", 
                    organization.Name, createdByUserId);

                return new OrganizationDto
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    Description = organization.Description,
                    CreatedAt = organization.CreatedAt,
                    IsActive = organization.IsActive,
                    CreatedByUserId = organization.CreatedByUserId
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create organization {OrganizationName}", createDto.Name);
                throw;
            }
        });
    }

    public async Task<OrganizationDto?> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto updateDto, int userId)
    {
        var organization = await _context.Organizations
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == id && o.IsActive);

        if (organization == null)
            return null;

        // Check if user is owner or admin
        var userRole = await GetUserRoleInOrganizationAsync(id, userId);
        if (userRole != Roles.OrganizationOwner && userRole != Roles.OrganizationAdmin)
            return null;

        if (!string.IsNullOrWhiteSpace(updateDto.Name))
            organization.Name = updateDto.Name;

        if (updateDto.Description != null)
            organization.Description = updateDto.Description;

        organization.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Organization {OrganizationId} updated by user {UserId}", id, userId);

        return new OrganizationDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Description = organization.Description,
            CreatedAt = organization.CreatedAt,
            IsActive = organization.IsActive,
            CreatedByUserId = organization.CreatedByUserId
        };
    }

    public async Task<bool> DeleteOrganizationAsync(Guid id, int userId)
    {
        var organization = await _context.Organizations
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == id && o.IsActive);

        if (organization == null)
            return false;

        // Only owner can delete organization
        var userRole = await GetUserRoleInOrganizationAsync(id, userId);
        if (userRole != Roles.OrganizationOwner)
            return false;

        organization.IsActive = false;
        organization.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Organization {OrganizationId} deleted by user {UserId}", id, userId);
        
        return true;
    }

    public async Task<bool> CanUserAccessOrganizationAsync(Guid organizationId, int userId)
    {
        return await _context.OrganizationUsers
            .AnyAsync(ou => ou.OrganizationId == organizationId && 
                           ou.UserId == userId && 
                           ou.IsActive);
    }

    public async Task<string?> GetUserRoleInOrganizationAsync(Guid organizationId, int userId)
    {
        var membership = await _context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && 
                                      ou.UserId == userId && 
                                      ou.IsActive);

        return membership?.Role;
    }
}