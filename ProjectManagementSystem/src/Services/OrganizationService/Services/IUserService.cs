using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.OrganizationService.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<IList<UserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds);
    Task<UserDto?> FindOrCreateUserAsync(string email, string firstName, string lastName);
    Task<UserDto?> CheckUserExistsByEmailAsync(string email);
    Task<UserDto?> CreateUserWithPasswordAsync(CreateUserWithPasswordRequest request);
}