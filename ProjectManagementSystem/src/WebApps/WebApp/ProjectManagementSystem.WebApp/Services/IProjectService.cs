using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.WebApp.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>?> GetProjectsAsync(int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<ProjectDto>?> GetProjectsByOrganizationAsync(int organizationId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectDto?> GetProjectAsync(int id);
    Task<ProjectDto?> CreateProjectAsync(CreateProjectDto projectDto);
    Task<ProjectDto?> UpdateProjectAsync(int id, UpdateProjectDto projectDto);
    Task<bool> DeleteProjectAsync(int id);
    Task<PagedResult<ProjectMemberDto>?> GetProjectMembersAsync(int projectId, int pageNumber = 1, int pageSize = 10);
}