using ProjectManagementSystem.IdentityService.Data.Entities;

namespace ProjectManagementSystem.IdentityService.Services;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user);
    string GenerateRefreshToken();
    Task<RefreshToken> CreateRefreshTokenAsync(int userId);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null);
    Task RevokeAllUserRefreshTokensAsync(int userId);
}