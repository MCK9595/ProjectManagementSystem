using System.Security.Claims;

namespace ProjectManagementSystem.Shared.Client.Authentication;

/// <summary>
/// Base class for authentication state providers
/// </summary>
public abstract class AuthenticationStateProvider
{
    /// <summary>
    /// Event that is raised when the authentication state has changed
    /// </summary>
    public event Func<Task<AuthenticationState>, Task>? AuthenticationStateChanged;

    /// <summary>
    /// Gets the current authentication state
    /// </summary>
    /// <returns>The current authentication state</returns>
    public abstract Task<AuthenticationState> GetAuthenticationStateAsync();

    /// <summary>
    /// Notifies components that the authentication state has changed
    /// </summary>
    /// <param name="authenticationStateTask">The new authentication state</param>
    protected void NotifyAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        AuthenticationStateChanged?.Invoke(authenticationStateTask);
    }
}