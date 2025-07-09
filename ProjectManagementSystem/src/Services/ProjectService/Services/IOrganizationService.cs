using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.ProjectService.Services;

public interface IOrganizationService
{
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId);
}