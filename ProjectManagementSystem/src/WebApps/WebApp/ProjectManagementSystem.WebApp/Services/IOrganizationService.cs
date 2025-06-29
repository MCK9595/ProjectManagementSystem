using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.WebApp.Services;

public interface IOrganizationService
{
    Task<PagedResult<OrganizationDto>?> GetOrganizationsAsync(int pageNumber = 1, int pageSize = 10);
    Task<OrganizationDto?> GetOrganizationAsync(int id);
    Task<OrganizationDto?> CreateOrganizationAsync(CreateOrganizationDto organizationDto);
    Task<OrganizationDto?> UpdateOrganizationAsync(int id, UpdateOrganizationDto organizationDto);
    Task<bool> DeleteOrganizationAsync(int id);
    Task<PagedResult<OrganizationMemberDto>?> GetOrganizationMembersAsync(int organizationId, int pageNumber = 1, int pageSize = 10);
}