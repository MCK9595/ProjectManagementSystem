using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;
using ProjectManagementSystem.Shared.Common.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ProjectManagementSystem.WebApp.Services;

public class ServerAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ISessionTokenService _sessionTokenService;
    private readonly IAuthService _authService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ServerAuthenticationStateProvider> _logger;
    private const string AuthStateCacheKey = "auth_state_cache";
    private const string LastTokenCacheKey = "last_token_cache";

    public ServerAuthenticationStateProvider(
        ISessionTokenService sessionTokenService,
        IAuthService authService,
        IMemoryCache memoryCache,
        ILogger<ServerAuthenticationStateProvider> logger)
    {
        _sessionTokenService = sessionTokenService;
        _authService = authService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            _logger.LogInformation("=== Getting Server Authentication State ===");
            
            // Check if we have a valid token first
            var currentToken = await _sessionTokenService.GetTokenAsync();
            
            // Check cached authentication state if token hasn't changed
            if (!string.IsNullOrEmpty(currentToken) && _memoryCache.TryGetValue(LastTokenCacheKey, out string? cachedToken))
            {
                if (currentToken == cachedToken && _memoryCache.TryGetValue(AuthStateCacheKey, out AuthenticationState? cachedState))
                {
                    _logger.LogDebug("Returning cached authentication state");
                    return cachedState;
                }
            }
            
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
                var authState = new AuthenticationState(principal);
                
                // Cache the authentication state and current token
                if (!string.IsNullOrEmpty(currentToken))
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
                    
                    _memoryCache.Set(AuthStateCacheKey, authState, cacheOptions);
                    _memoryCache.Set(LastTokenCacheKey, currentToken, cacheOptions);
                    _logger.LogDebug("Authentication state cached");
                }
                
                _logger.LogInformation("Authentication state: AUTHENTICATED");
                return authState;
            }
            else
            {
                _logger.LogInformation("No user found, authentication state: NOT AUTHENTICATED");
                // Clear cache for unauthenticated state
                ClearAuthenticationCache();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetAuthenticationStateAsync");
            // Clear cache on exception
            ClearAuthenticationCache();
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
        
        // Clear authentication cache on logout
        ClearAuthenticationCache();
        
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
        _logger.LogInformation("Logout notification sent");
    }
    
    private void ClearAuthenticationCache()
    {
        try
        {
            _memoryCache.Remove(AuthStateCacheKey);
            _memoryCache.Remove(LastTokenCacheKey);
            _logger.LogDebug("Authentication cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear authentication cache");
        }
    }
    
    // Keep the synchronous version for backward compatibility but mark as obsolete
    [Obsolete("Use NotifyUserAuthenticationAsync() instead for better reliability")]
    public void NotifyUserAuthentication()
    {
        Task.Run(async () => await NotifyUserAuthenticationAsync());
    }
}