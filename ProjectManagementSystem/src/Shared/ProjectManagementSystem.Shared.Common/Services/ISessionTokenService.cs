namespace ProjectManagementSystem.Shared.Common.Services;

public interface ISessionTokenService
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
    Task FlushPendingTokenAsync(); // For client-side token persistence after prerendering
}