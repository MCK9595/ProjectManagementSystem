namespace ProjectManagementSystem.Shared.Client.Services;

public interface ISessionTokenService
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
    Task FlushPendingTokenAsync(); // For client-side token persistence
}