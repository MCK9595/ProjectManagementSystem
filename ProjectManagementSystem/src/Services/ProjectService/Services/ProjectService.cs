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

    public ProjectService(ProjectDbContext context, ILogger<ProjectService> logger)
    {
        _context = context;
        _logger = logger;
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

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto, int createdByUserId, string? userName = null, string? userEmail = null)
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
            Role = Roles.ProjectManager,
            UserName = userName,
            UserEmail = userEmail
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
}