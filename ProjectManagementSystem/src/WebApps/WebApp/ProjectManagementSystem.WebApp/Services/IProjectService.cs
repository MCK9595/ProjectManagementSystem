using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.WebApp.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>?> GetProjectsAsync(int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<ProjectDto>?> GetProjectsByOrganizationAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectDto?> GetProjectAsync(Guid id);
    Task<ProjectDto?> CreateProjectAsync(CreateProjectDto projectDto);
    Task<ProjectDto?> UpdateProjectAsync(Guid id, UpdateProjectDto projectDto);
    Task<bool> DeleteProjectAsync(Guid id);
    Task<PagedResult<ProjectMemberDto>?> GetProjectMembersAsync(Guid projectId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectMemberDto?> AddMemberAsync(Guid projectId, AddProjectMemberDto addMemberDto);
    Task<bool> RemoveMemberAsync(Guid projectId, int userId);
    Task<bool> UpdateMemberRoleAsync(Guid projectId, int userId, string role);
}