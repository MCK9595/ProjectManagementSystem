using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;
using ProjectManagementSystem.Shared.Common.Services;

namespace ProjectManagementSystem.WebApp.Services;

public class ServerAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ISessionTokenService _sessionTokenService;
    private readonly IAuthService _authService;
    private readonly ILogger<ServerAuthenticationStateProvider> _logger;

    public ServerAuthenticationStateProvider(
        ISessionTokenService sessionTokenService,
        IAuthService authService,
        ILogger<ServerAuthenticationStateProvider> logger)
    {
        _sessionTokenService = sessionTokenService;
        _authService = authService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            _logger.LogInformation("=== Getting Server Authentication State ===");
            
            var user = await _authService.GetCurrentUserAsync();
            
            if (user != null)
            {
                _logger.LogInformation("User authenticated: {Username} (ID: {Id}, Role: {Role})", 
                    user.Username, user.Id, user.Role);
                    
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.Username),
                    new(ClaimTypes.Email, user.Email ?? ""),
                    new(ClaimTypes.GivenName, user.FirstName),
                    new(ClaimTypes.Surname, user.LastName),
                    new(ClaimTypes.Role, user.Role)
                };

                var identity = new ClaimsIdentity(claims, "session");
                var principal = new ClaimsPrincipal(identity);
                
                _logger.LogInformation("Authentication state: AUTHENTICATED");
                return new AuthenticationState(principal);
            }
            else
            {
                _logger.LogInformation("No user found, authentication state: NOT AUTHENTICATED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetAuthenticationStateAsync");
        }

        _logger.LogInformation("Returning anonymous authentication state");
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public async Task NotifyUserAuthenticationAsync()
    {
        _logger.LogInformation("=== Notifying Authentication State Change ===");
        
        try
        {
            // Try to flush any pending tokens first
            await _sessionTokenService.FlushPendingTokenAsync();
            _logger.LogDebug("Pending tokens flushed before authentication state refresh");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush pending tokens before authentication state refresh");
        }
        
        // Force refresh the authentication state
        var authState = await GetAuthenticationStateAsync();
        
        _logger.LogInformation("New auth state - IsAuthenticated: {IsAuthenticated}, Name: {Name}, Role: {Role}", 
            authState.User.Identity?.IsAuthenticated ?? false,
            authState.User.Identity?.Name ?? "NULL",
            authState.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "NULL");
        
        // Verify token availability for debugging
        try
        {
            var token = await _sessionTokenService.GetTokenAsync();
            _logger.LogDebug("Token available during notification: {HasToken}, Length: {Length}", 
                !string.IsNullOrEmpty(token), token?.Length ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check token availability during notification");
        }
            
        NotifyAuthenticationStateChanged(Task.FromResult(authState));
        
        _logger.LogInformation("Authentication state change notification sent");
    }

    public void NotifyUserLogout()
    {
        _logger.LogInformation("=== Notifying User Logout ===");
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
        _logger.LogInformation("Logout notification sent");
    }
    
    // Keep the synchronous version for backward compatibility but mark as obsolete
    [Obsolete("Use NotifyUserAuthenticationAsync() instead for better reliability")]
    public void NotifyUserAuthentication()
    {
        Task.Run(async () => await NotifyUserAuthenticationAsync());
    }
}