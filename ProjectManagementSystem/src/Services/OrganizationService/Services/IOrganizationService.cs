using ProjectManagementSystem.OrganizationService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.OrganizationService.Services;

public interface IOrganizationService
{
    Task<PagedResult<OrganizationDto>> GetOrganizationsAsync(int userId, int page, int pageSize);
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid id, int userId);
    Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationDto createDto, int createdByUserId);
    Task<OrganizationDto?> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto updateDto, int userId);
    Task<bool> DeleteOrganizationAsync(Guid id, int userId);
    Task<bool> CanUserAccessOrganizationAsync(Guid organizationId, int userId);
    Task<string?> GetUserRoleInOrganizationAsync(Guid organizationId, int userId);
    
    // User deletion support methods
    Task<bool> HasUserBlockingAdminRolesAsync(int userId);
    Task<bool> CleanupUserDependenciesAsync(int userId);
}

public interface IOrganizationMemberService
{
    Task<PagedResult<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, int requestingUserId, int page, int pageSize);
    Task<OrganizationMemberDto?> AddMemberAsync(Guid organizationId, AddMemberDto addMemberDto, int requestingUserId);
    Task<OrganizationMemberDto?> AddMemberByEmailAsync(Guid organizationId, AddMemberByEmailDto addMemberDto, int requestingUserId);
    Task<OrganizationMemberDto?> AddExistingUserByEmailAsync(Guid organizationId, FindUserByEmailDto findUserDto, int requestingUserId);
    Task<OrganizationMemberDto?> CreateUserAndAddMemberAsync(Guid organizationId, CreateUserAndAddMemberDto createUserDto, int requestingUserId);
    Task<bool> RemoveMemberAsync(Guid organizationId, int userId, int requestingUserId);
    Task<bool> UpdateMemberRoleAsync(Guid organizationId, int userId, string newRole, int requestingUserId);
    Task<bool> TransferOwnershipAsync(Guid organizationId, int newOwnerId, int currentOwnerId);
}