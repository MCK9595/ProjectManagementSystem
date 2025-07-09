using Microsoft.Extensions.Caching.Memory;
using ProjectManagementSystem.Shared.Common.Services;
using System.Security.Claims;

namespace ProjectManagementSystem.WebApp.Services;

public class MemoryTokenService : ISessionTokenService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MemoryTokenService> _logger;
    private const string TokenKeyPrefix = "auth_token_";
    
    public MemoryTokenService(
        IMemoryCache memoryCache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MemoryTokenService> logger)
    {
        _memoryCache = memoryCache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _logger.LogDebug("MemoryTokenService initialized");
    }

    public async Task<string?> GetTokenAsync()
    {
        _logger.LogDebug("Getting token from memory cache");
        
        try
        {
            // Ensure session is loaded first
            await EnsureSessionLoadedAsync();
            
            // Primary method: session-based token storage
            var sessionId = GetSessionId();
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                if (_memoryCache.TryGetValue(sessionKey, out string? sessionToken))
                {
                    _logger.LogDebug("Token retrieved from session cache {SessionId}, Length: {Length}", 
                        sessionId, sessionToken?.Length ?? 0);
                    return sessionToken;
                }
                else
                {
                    _logger.LogDebug("No token found in session cache for session {SessionId}", sessionId);
                }
            }
            else
            {
                _logger.LogDebug("Session ID not available");
            }
            
            // Secondary method: user-based token (only if already authenticated)
            var userId = GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                var userKey = $"{TokenKeyPrefix}user_{userId}";
                if (_memoryCache.TryGetValue(userKey, out string? userToken))
                {
                    _logger.LogDebug("Token retrieved from user cache {UserId}, Length: {Length}", 
                        userId, userToken?.Length ?? 0);
                    
                    // If we found a user token but no session token, copy it to session cache
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromHours(8))
                            .SetAbsoluteExpiration(TimeSpan.FromHours(24));
                        _memoryCache.Set(sessionKey, userToken, cacheOptions);
                        _logger.LogDebug("Copied user token to session cache");
                    }
                    
                    return userToken;
                }
            }
            
            _logger.LogDebug("No token found in cache - sessionId: {SessionId}, userId: {UserId}", 
                sessionId ?? "null", userId ?? "null");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting token from cache");
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Attempted to set null or empty token");
            return;
        }
        
        _logger.LogInformation("Setting token in memory cache, Length: {Length}", token.Length);
        
        try
        {
            // Ensure session is loaded first
            await EnsureSessionLoadedAsync();
            
            var sessionId = GetSessionId();
            var userId = GetUserId();
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(8))
                .SetAbsoluteExpiration(TimeSpan.FromHours(24));
            
            bool tokenStored = false;
            
            // Primary storage: session-based (works for both authenticated and unauthenticated users)
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                _memoryCache.Set(sessionKey, token, cacheOptions);
                _logger.LogInformation("Token stored in session cache {SessionId}", sessionId);
                tokenStored = true;
            }
            else
            {
                _logger.LogWarning("Session ID not available for token storage");
            }
            
            // Secondary storage: user-based (for authenticated users)
            if (!string.IsNullOrEmpty(userId))
            {
                var userKey = $"{TokenKeyPrefix}user_{userId}";
                _memoryCache.Set(userKey, token, cacheOptions);
                _logger.LogInformation("Token also stored in user cache {UserId}", userId);
                tokenStored = true;
            }
            
            // Fallback: if neither session nor user ID available, use a temporary key
            if (!tokenStored)
            {
                var tempKey = $"{TokenKeyPrefix}temp_{DateTime.UtcNow.Ticks}";
                _memoryCache.Set(tempKey, token, cacheOptions);
                _logger.LogWarning("Token stored with temporary key - session and user ID not available");
            }
            
            _logger.LogDebug("Token storage complete - sessionId: {SessionId}, userId: {UserId}", 
                sessionId ?? "null", userId ?? "null");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception setting token in cache");
        }
    }

    public async Task RemoveTokenAsync()
    {
        _logger.LogInformation("Removing token from memory cache");
        
        try
        {
            // Ensure session is loaded first
            await EnsureSessionLoadedAsync();
            
            var userId = GetUserId();
            var sessionId = GetSessionId();
            
            bool tokenRemoved = false;
            
            // Remove from session cache
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                _memoryCache.Remove(sessionKey);
                _logger.LogDebug("Token removed from session cache {SessionId}", sessionId);
                tokenRemoved = true;
            }
            
            // Remove from user cache
            if (!string.IsNullOrEmpty(userId))
            {
                var userKey = $"{TokenKeyPrefix}user_{userId}";
                _memoryCache.Remove(userKey);
                _logger.LogDebug("Token removed from user cache {UserId}", userId);
                tokenRemoved = true;
            }
            
            if (!tokenRemoved)
            {
                _logger.LogWarning("Could not remove token - no session or user ID available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception removing token from cache");
        }
    }
    
    public Task FlushPendingTokenAsync()
    {
        _logger.LogDebug("FlushPendingTokenAsync called - no action needed for memory cache implementation");
        return Task.CompletedTask;
    }
    
    private string? GetUserId()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? user.FindFirst("id")?.Value 
                           ?? user.FindFirst("sub")?.Value;
                           
                if (!string.IsNullOrEmpty(userId))
                {
                    _logger.LogDebug("User ID retrieved: {UserId}", userId);
                    return userId;
                }
            }
            
            _logger.LogDebug("User not authenticated or no user ID found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user ID");
            return null;
        }
    }
    
    private string? GetSessionId()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session != null)
            {
                // Check if session is available
                if (httpContext.Session.IsAvailable)
                {
                    var sessionId = httpContext.Session.Id;
                    _logger.LogDebug("Session ID retrieved: {SessionId}", sessionId);
                    return sessionId;
                }
                else
                {
                    _logger.LogDebug("Session not available");
                }
            }
            else
            {
                _logger.LogDebug("HttpContext or Session is null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get session ID");
        }
        return null;
    }
    
    private async Task EnsureSessionLoadedAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session != null && !httpContext.Session.IsAvailable)
            {
                _logger.LogDebug("Loading session");
                await httpContext.Session.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load session");
        }
    }
}