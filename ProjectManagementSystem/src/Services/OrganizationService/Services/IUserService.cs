using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.OrganizationService.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<IList<UserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds);
}