using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.ProjectService.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetProjectsAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<ProjectDto>> GetProjectsByUserAsync(int userId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectDto?> GetProjectByIdAsync(Guid projectId);
    Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto, int createdByUserId);
    Task<ProjectDto?> UpdateProjectAsync(Guid projectId, UpdateProjectDto updateProjectDto);
    Task<bool> DeleteProjectAsync(Guid projectId);
    Task<bool> ArchiveProjectAsync(Guid projectId);
    Task<bool> RestoreProjectAsync(Guid projectId);
    Task<bool> HasProjectAccessAsync(Guid projectId, int userId);
    Task<bool> IsProjectManagerAsync(Guid projectId, int userId);
    
    // User deletion support methods
    Task<bool> HasUserBlockingAdminRolesAsync(int userId);
    Task<bool> CleanupUserDependenciesAsync(int userId);
}