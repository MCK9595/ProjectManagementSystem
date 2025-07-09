using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.ProjectService.Data;
using ProjectManagementSystem.ProjectService.Data.Entities;
using ProjectManagementSystem.Shared.Common.Constants;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.ProjectService.Services;

public class ProjectService : IProjectService
{
    private readonly ProjectDbContext _context;
    private readonly ILogger<ProjectService> _logger;
    private readonly IOrganizationService _organizationService;

    public ProjectService(ProjectDbContext context, ILogger<ProjectService> logger, IOrganizationService organizationService)
    {
        _context = context;
        _logger = logger;
        _organizationService = organizationService;
    }

    public async Task<PagedResult<ProjectDto>> GetProjectsAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Projects
            .Where(p => p.OrganizationId == organizationId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync();
        var projects = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Status = p.Status,
                Priority = p.Priority,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                OrganizationId = p.OrganizationId,
                CreatedByUserId = p.CreatedByUserId
            })
            .ToListAsync();

        return new PagedResult<ProjectDto>
        {
            Items = projects,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<ProjectDto>> GetProjectsByUserAsync(int userId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Projects
            .Where(p => p.IsActive && p.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync();
        var projects = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Status = p.Status,
                Priority = p.Priority,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                OrganizationId = p.OrganizationId,
                CreatedByUserId = p.CreatedByUserId
            })
            .ToListAsync();

        return new PagedResult<ProjectDto>
        {
            Items = projects,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ProjectDto?> GetProjectByIdAsync(Guid projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId && p.IsActive)
            .FirstOrDefaultAsync();

        if (project == null)
            return null;

        // Load organization information
        OrganizationDto? organization = null;
        try
        {
            organization = await _organizationService.GetOrganizationByIdAsync(project.OrganizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load organization {OrganizationId} for project {ProjectId}", 
                project.OrganizationId, projectId);
        }

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            Priority = project.Priority,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            OrganizationId = project.OrganizationId,
            CreatedByUserId = project.CreatedByUserId,
            Organization = organization
        };
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto, int createdByUserId)
    {
        var project = new Project
        {
            Name = createProjectDto.Name,
            Description = createProjectDto.Description,
            Status = createProjectDto.Status,
            Priority = createProjectDto.Priority,
            StartDate = createProjectDto.StartDate,
            EndDate = createProjectDto.EndDate,
            OrganizationId = createProjectDto.OrganizationId,
            CreatedByUserId = createdByUserId
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var projectMember = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = createdByUserId,
            Role = Roles.ProjectManager
        };

        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Project {ProjectName} created by user {UserId}", project.Name, createdByUserId);

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            Priority = project.Priority,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            OrganizationId = project.OrganizationId,
            CreatedByUserId = project.CreatedByUserId
        };
    }

    public async Task<ProjectDto?> UpdateProjectAsync(Guid projectId, UpdateProjectDto updateProjectDto)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId && p.IsActive)
            .FirstOrDefaultAsync();

        if (project == null)
            return null;

        if (!string.IsNullOrEmpty(updateProjectDto.Name))
            project.Name = updateProjectDto.Name;

        if (updateProjectDto.Description != null)
            project.Description = updateProjectDto.Description;

        if (!string.IsNullOrEmpty(updateProjectDto.Status))
            project.Status = updateProjectDto.Status;

        if (!string.IsNullOrEmpty(updateProjectDto.Priority))
            project.Priority = updateProjectDto.Priority;

        if (updateProjectDto.StartDate.HasValue)
            project.StartDate = updateProjectDto.StartDate;

        if (updateProjectDto.EndDate.HasValue)
            project.EndDate = updateProjectDto.EndDate;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Project {ProjectId} updated", projectId);

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            Priority = project.Priority,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            OrganizationId = project.OrganizationId,
            CreatedByUserId = project.CreatedByUserId
        };
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId && p.IsActive)
            .FirstOrDefaultAsync();

        if (project == null)
            return false;

        project.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Project {ProjectId} deleted (soft delete)", projectId);
        return true;
    }

    public async Task<bool> ArchiveProjectAsync(Guid projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId && p.IsActive)
            .FirstOrDefaultAsync();

        if (project == null)
            return false;

        project.Status = ProjectStatus.Completed;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Project {ProjectId} archived", projectId);
        return true;
    }

    public async Task<bool> RestoreProjectAsync(Guid projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId)
            .FirstOrDefaultAsync();

        if (project == null)
            return false;

        project.IsActive = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Project {ProjectId} restored", projectId);
        return true;
    }

    public async Task<bool> HasProjectAccessAsync(Guid projectId, int userId)
    {
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive);
    }

    public async Task<bool> IsProjectManagerAsync(Guid projectId, int userId)
    {
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && 
                           pm.Role == Roles.ProjectManager && pm.IsActive);
    }

    public async Task<bool> HasUserBlockingAdminRolesAsync(int userId)
    {
        try
        {
            _logger.LogInformation("Checking if user {UserId} has blocking project admin roles", userId);

            // Find projects where the user is the sole manager
            var projectsWhereUserIsSoleManager = await _context.Projects
                .Where(p => p.IsActive)
                .Where(p => p.Members.Any(m => m.UserId == userId && m.IsActive && m.Role == Roles.ProjectManager))
                .Where(p => p.Members.Count(m => m.IsActive && m.Role == Roles.ProjectManager) == 1)
                .CountAsync();

            var hasBlockingRoles = projectsWhereUserIsSoleManager > 0;
            
            _logger.LogInformation("User {UserId} has {Count} projects where they are sole manager - blocking: {IsBlocking}", 
                userId, projectsWhereUserIsSoleManager, hasBlockingRoles);

            return hasBlockingRoles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking blocking project admin roles for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> CleanupUserDependenciesAsync(int userId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                _logger.LogInformation("Starting project dependency cleanup for user {UserId}", userId);

                // First, check if user has any blocking roles
                var hasBlockingRoles = await HasUserBlockingAdminRolesAsync(userId);
                if (hasBlockingRoles)
                {
                    _logger.LogError("Cannot cleanup dependencies - user {UserId} is sole manager of projects", userId);
                    throw new InvalidOperationException("User is the sole manager of one or more projects. Transfer ownership before deletion.");
                }

                // Find all project memberships for the user
                var userMemberships = await _context.ProjectMembers
                    .Where(pm => pm.UserId == userId && pm.IsActive)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} project memberships to clean up for user {UserId}", 
                    userMemberships.Count, userId);

                // Soft delete all memberships
                foreach (var membership in userMemberships)
                {
                    membership.IsActive = false;
                    _logger.LogDebug("Removing user {UserId} from project {ProjectId}", 
                        userId, membership.ProjectId);
                }

                // Save changes
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully cleaned up {Count} project dependencies for user {UserId}", 
                    userMemberships.Count, userId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to cleanup project dependencies for user {UserId}", userId);
                throw;
            }
        });
    }
}