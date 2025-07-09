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

    public Task<string?> GetTokenAsync()
    {
        _logger.LogDebug("Getting token from memory cache");
        
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            var sessionId = GetSessionId();
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                if (_memoryCache.TryGetValue(sessionKey, out string? sessionToken))
                {
                    _logger.LogDebug("Token retrieved from cache for session {SessionId}, Length: {Length}", 
                        sessionId, sessionToken?.Length ?? 0);
                    return Task.FromResult(sessionToken);
                }
            }
            
            _logger.LogDebug("No user ID or session token found - user not authenticated");
            return Task.FromResult<string?>(null);
        }
        
        var cacheKey = $"{TokenKeyPrefix}{userId}";
        
        if (_memoryCache.TryGetValue(cacheKey, out string? token))
        {
            _logger.LogDebug("Token retrieved from cache for user {UserId}, Length: {Length}", 
                userId, token?.Length ?? 0);
            return Task.FromResult(token);
        }
        
        _logger.LogDebug("No token found in cache for user {UserId}", userId);
        return Task.FromResult<string?>(null);
    }

    public Task SetTokenAsync(string token)
    {
        _logger.LogInformation("Setting token in memory cache");
        
        var userId = GetUserId();
        var sessionId = GetSessionId();
        
        if (string.IsNullOrEmpty(userId))
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                var options = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(8))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(24));
                    
                _memoryCache.Set(sessionKey, token, options);
                _logger.LogInformation("Token stored in cache for session {SessionId}, Length: {Length}", 
                    sessionId, token.Length);
            }
            else
            {
                _logger.LogWarning("Cannot store token - no user ID or session ID available");
            }
            return Task.CompletedTask;
        }
        
        var cacheKey = $"{TokenKeyPrefix}{userId}";
        
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromHours(8))
            .SetAbsoluteExpiration(TimeSpan.FromHours(24));
        
        _memoryCache.Set(cacheKey, token, cacheOptions);
        _logger.LogInformation("Token successfully stored in cache for user {UserId}, Length: {Length}", 
            userId, token.Length);
        
        return Task.CompletedTask;
    }

    public Task RemoveTokenAsync()
    {
        _logger.LogInformation("Removing token from memory cache");
        
        var userId = GetUserId();
        var sessionId = GetSessionId();
        
        if (!string.IsNullOrEmpty(userId))
        {
            var cacheKey = $"{TokenKeyPrefix}{userId}";
            _memoryCache.Remove(cacheKey);
            _logger.LogDebug("Token removed from cache for user {UserId}", userId);
        }
        
        if (!string.IsNullOrEmpty(sessionId))
        {
            var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
            _memoryCache.Remove(sessionKey);
            _logger.LogDebug("Token removed from cache for session {SessionId}", sessionId);
        }
        
        return Task.CompletedTask;
    }
    
    public Task FlushPendingTokenAsync()
    {
        _logger.LogDebug("FlushPendingTokenAsync called - no action needed for memory cache implementation");
        return Task.CompletedTask;
    }
    
    private string? GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        return null;
    }
    
    private string? GetSessionId()
    {
        try
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session?.IsAvailable == true)
            {
                return session.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get session ID");
        }
        return null;
    }
}