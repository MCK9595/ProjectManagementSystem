using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.ProjectService.Services;

public interface IProjectMemberService
{
    Task<PagedResult<ProjectMemberDto>> GetProjectMembersAsync(int projectId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectMemberDto?> GetProjectMemberAsync(int projectId, int userId);
    Task<ProjectMemberDto> AddProjectMemberAsync(int projectId, int userId, string role);
    Task<ProjectMemberDto?> UpdateProjectMemberRoleAsync(int projectId, int userId, string role);
    Task<bool> RemoveProjectMemberAsync(int projectId, int userId);
    Task<bool> IsProjectMemberAsync(int projectId, int userId);
    Task<string?> GetProjectMemberRoleAsync(int projectId, int userId);
}