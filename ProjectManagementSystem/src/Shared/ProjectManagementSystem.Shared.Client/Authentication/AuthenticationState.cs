using System.Security.Claims;

namespace ProjectManagementSystem.Shared.Client.Authentication;

/// <summary>
/// Represents the authentication state of a user
/// </summary>
public class AuthenticationState
{
    /// <summary>
    /// Creates a new instance of AuthenticationState
    /// </summary>
    /// <param name="user">The user's claims principal</param>
    public AuthenticationState(ClaimsPrincipal user)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
    }

    /// <summary>
    /// The user's claims principal
    /// </summary>
    public ClaimsPrincipal User { get; }
}