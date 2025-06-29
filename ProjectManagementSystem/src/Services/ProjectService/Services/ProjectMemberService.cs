using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.ProjectService.Data;
using ProjectManagementSystem.ProjectService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.ProjectService.Services;

public class ProjectMemberService : IProjectMemberService
{
    private readonly ProjectDbContext _context;
    private readonly ILogger<ProjectMemberService> _logger;

    public ProjectMemberService(ProjectDbContext context, ILogger<ProjectMemberService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ProjectMemberDto>> GetProjectMembersAsync(int projectId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.IsActive)
            .OrderBy(pm => pm.JoinedAt);

        var totalCount = await query.CountAsync();
        var members = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(pm => new ProjectMemberDto
            {
                Id = pm.Id,
                ProjectId = pm.ProjectId,
                UserId = pm.UserId,
                Role = pm.Role,
                JoinedAt = pm.JoinedAt
            })
            .ToListAsync();

        return new PagedResult<ProjectMemberDto>
        {
            Items = members,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ProjectMemberDto?> GetProjectMemberAsync(int projectId, int userId)
    {
        var member = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive)
            .FirstOrDefaultAsync();

        if (member == null)
            return null;

        return new ProjectMemberDto
        {
            Id = member.Id,
            ProjectId = member.ProjectId,
            UserId = member.UserId,
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };
    }

    public async Task<ProjectMemberDto> AddProjectMemberAsync(int projectId, int userId, string role)
    {
        var existingMember = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId == userId)
            .FirstOrDefaultAsync();

        if (existingMember != null)
        {
            if (!existingMember.IsActive)
            {
                existingMember.IsActive = true;
                existingMember.Role = role;
                existingMember.JoinedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Project member {UserId} reactivated for project {ProjectId}", userId, projectId);

                return new ProjectMemberDto
                {
                    Id = existingMember.Id,
                    ProjectId = existingMember.ProjectId,
                    UserId = existingMember.UserId,
                    Role = existingMember.Role,
                    JoinedAt = existingMember.JoinedAt
                };
            }
            else
            {
                throw new InvalidOperationException("User is already a member of this project");
            }
        }

        var projectMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role
        };

        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} added to project {ProjectId} with role {Role}", userId, projectId, role);

        return new ProjectMemberDto
        {
            Id = projectMember.Id,
            ProjectId = projectMember.ProjectId,
            UserId = projectMember.UserId,
            Role = projectMember.Role,
            JoinedAt = projectMember.JoinedAt
        };
    }

    public async Task<ProjectMemberDto?> UpdateProjectMemberRoleAsync(int projectId, int userId, string role)
    {
        var member = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive)
            .FirstOrDefaultAsync();

        if (member == null)
            return null;

        member.Role = role;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Project member {UserId} role updated to {Role} for project {ProjectId}", userId, role, projectId);

        return new ProjectMemberDto
        {
            Id = member.Id,
            ProjectId = member.ProjectId,
            UserId = member.UserId,
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };
    }

    public async Task<bool> RemoveProjectMemberAsync(int projectId, int userId)
    {
        var member = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive)
            .FirstOrDefaultAsync();

        if (member == null)
            return false;

        member.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} removed from project {ProjectId}", userId, projectId);
        return true;
    }

    public async Task<bool> IsProjectMemberAsync(int projectId, int userId)
    {
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive);
    }

    public async Task<string?> GetProjectMemberRoleAsync(int projectId, int userId)
    {
        var member = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive)
            .FirstOrDefaultAsync();

        return member?.Role;
    }
}