using ProjectManagementSystem.OrganizationService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.OrganizationService.Services;

public interface IOrganizationService
{
    Task<PagedResult<OrganizationDto>> GetOrganizationsAsync(int userId, int page, int pageSize);
    Task<OrganizationDto?> GetOrganizationByIdAsync(int id, int userId);
    Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationDto createDto, int createdByUserId);
    Task<OrganizationDto?> UpdateOrganizationAsync(int id, UpdateOrganizationDto updateDto, int userId);
    Task<bool> DeleteOrganizationAsync(int id, int userId);
    Task<bool> CanUserAccessOrganizationAsync(int organizationId, int userId);
    Task<string?> GetUserRoleInOrganizationAsync(int organizationId, int userId);
}

public interface IOrganizationMemberService
{
    Task<PagedResult<OrganizationMemberDto>> GetMembersAsync(int organizationId, int requestingUserId, int page, int pageSize);
    Task<OrganizationMemberDto?> AddMemberAsync(int organizationId, AddMemberDto addMemberDto, int requestingUserId);
    Task<bool> RemoveMemberAsync(int organizationId, int userId, int requestingUserId);
    Task<bool> UpdateMemberRoleAsync(int organizationId, int userId, string newRole, int requestingUserId);
}