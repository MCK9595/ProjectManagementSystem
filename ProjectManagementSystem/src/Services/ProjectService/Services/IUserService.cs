using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.ProjectService.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<IList<UserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds);
}