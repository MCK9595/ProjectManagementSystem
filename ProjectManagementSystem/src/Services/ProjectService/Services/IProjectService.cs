using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.ProjectService.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetProjectsAsync(int organizationId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectDto?> GetProjectByIdAsync(int projectId);
    Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto, int createdByUserId);
    Task<ProjectDto?> UpdateProjectAsync(int projectId, UpdateProjectDto updateProjectDto);
    Task<bool> DeleteProjectAsync(int projectId);
    Task<bool> ArchiveProjectAsync(int projectId);
    Task<bool> RestoreProjectAsync(int projectId);
    Task<bool> HasProjectAccessAsync(int projectId, int userId);
    Task<bool> IsProjectManagerAsync(int projectId, int userId);
}