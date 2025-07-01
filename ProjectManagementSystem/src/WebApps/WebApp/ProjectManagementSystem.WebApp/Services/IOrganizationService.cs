using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.WebApp.Services;

public interface IOrganizationService
{
    Task<PagedResult<OrganizationDto>?> GetOrganizationsAsync(int pageNumber = 1, int pageSize = 10);
    Task<OrganizationDto?> GetOrganizationAsync(Guid id);
    Task<OrganizationDto?> CreateOrganizationAsync(CreateOrganizationDto organizationDto);
    Task<OrganizationDto?> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto organizationDto);
    Task<bool> DeleteOrganizationAsync(Guid id);
    Task<PagedResult<OrganizationMemberDto>?> GetOrganizationMembersAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10);
    Task<OrganizationMemberDto?> AddMemberAsync(Guid organizationId, AddMemberDto addMemberDto);
    Task<bool> RemoveMemberAsync(Guid organizationId, int userId);
    Task<bool> UpdateMemberRoleAsync(Guid organizationId, int userId, string role);
}