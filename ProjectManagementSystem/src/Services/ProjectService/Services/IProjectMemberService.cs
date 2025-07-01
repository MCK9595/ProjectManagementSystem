using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.ProjectService.Services;

public interface IProjectMemberService
{
    Task<PagedResult<ProjectMemberDto>> GetProjectMembersAsync(Guid projectId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectMemberDto?> GetProjectMemberAsync(Guid projectId, int userId);
    Task<ProjectMemberDto> AddProjectMemberAsync(Guid projectId, int userId, string role);
    Task<ProjectMemberDto?> UpdateProjectMemberRoleAsync(Guid projectId, int userId, string role);
    Task<bool> RemoveProjectMemberAsync(Guid projectId, int userId);
    Task<bool> IsProjectMemberAsync(Guid projectId, int userId);
    Task<string?> GetProjectMemberRoleAsync(Guid projectId, int userId);
}