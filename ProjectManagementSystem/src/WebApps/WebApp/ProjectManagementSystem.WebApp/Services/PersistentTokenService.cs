using Microsoft.Extensions.Caching.Memory;
using ProjectManagementSystem.Shared.Common.Services;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ProjectManagementSystem.WebApp.Services;

public class PersistentTokenService : ISessionTokenService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _dataProtector;
    private readonly ILogger<PersistentTokenService> _logger;
    private const string TokenKeyPrefix = "auth_token_";
    private const string TokenCookieName = "pms_auth_token";
    private const string TokenSessionKey = "pms_session_token";
    
    public PersistentTokenService(
        IMemoryCache memoryCache,
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<PersistentTokenService> logger)
    {
        _memoryCache = memoryCache;
        _httpContextAccessor = httpContextAccessor;
        _dataProtector = dataProtectionProvider.CreateProtector("ProjectManagement.AuthTokens");
        _logger = logger;
        _logger.LogDebug("PersistentTokenService initialized");
    }

    public async Task<string?> GetTokenAsync()
    {
        _logger.LogDebug("Getting token from persistent storage");
        
        try
        {
            // Ensure session is loaded first
            await EnsureSessionLoadedAsync();
            
            // Priority 1: Memory cache (fastest)
            var memoryToken = await GetTokenFromMemoryAsync();
            if (!string.IsNullOrEmpty(memoryToken))
            {
                _logger.LogDebug("Token retrieved from memory cache, Length: {Length}", memoryToken.Length);
                return memoryToken;
            }
            
            // Priority 2: Session storage
            var sessionToken = GetTokenFromSession();
            if (!string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogDebug("Token retrieved from session, Length: {Length}", sessionToken.Length);
                // Cache in memory for faster subsequent access
                await CacheTokenInMemoryAsync(sessionToken);
                return sessionToken;
            }
            
            // Priority 3: Encrypted cookie storage (persistent across browser sessions)
            var cookieToken = GetTokenFromCookie();
            if (!string.IsNullOrEmpty(cookieToken))
            {
                _logger.LogDebug("Token retrieved from encrypted cookie, Length: {Length}", cookieToken.Length);
                // Cache in memory and session for faster subsequent access
                await CacheTokenInMemoryAsync(cookieToken);
                SetTokenInSession(cookieToken);
                return cookieToken;
            }
            
            _logger.LogDebug("No token found in any storage");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting token from persistent storage");
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
        
        _logger.LogInformation("Setting token in persistent storage, Length: {Length}", token.Length);
        
        try
        {
            // Ensure session is loaded first
            await EnsureSessionLoadedAsync();
            
            // Store in memory cache (fastest access)
            await CacheTokenInMemoryAsync(token);
            
            // Store in session (survives circuit reconnections)
            SetTokenInSession(token);
            
            // Store in encrypted cookie (survives browser restarts)
            SetTokenInCookie(token);
            
            _logger.LogInformation("Token stored in all storage layers successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception setting token in persistent storage");
        }
    }

    public async Task RemoveTokenAsync()
    {
        _logger.LogInformation("Removing token from persistent storage");
        
        try
        {
            // Ensure session is loaded first
            await EnsureSessionLoadedAsync();
            
            // Remove from memory cache
            await RemoveTokenFromMemoryAsync();
            
            // Remove from session
            RemoveTokenFromSession();
            
            // Remove from cookie
            RemoveTokenFromCookie();
            
            _logger.LogInformation("Token removed from all storage layers successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception removing token from persistent storage");
        }
    }
    
    public Task FlushPendingTokenAsync()
    {
        _logger.LogDebug("FlushPendingTokenAsync called - no action needed for persistent storage implementation");
        return Task.CompletedTask;
    }
    
    private async Task<string?> GetTokenFromMemoryAsync()
    {
        try
        {
            var sessionId = GetSessionId();
            var userId = GetUserId();
            
            // Try session-based cache first
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                if (_memoryCache.TryGetValue(sessionKey, out string? sessionToken))
                {
                    return sessionToken;
                }
            }
            
            // Fallback to user-based cache
            if (!string.IsNullOrEmpty(userId))
            {
                var userKey = $"{TokenKeyPrefix}user_{userId}";
                if (_memoryCache.TryGetValue(userKey, out string? userToken))
                {
                    return userToken;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get token from memory");
            return null;
        }
    }
    
    private async Task CacheTokenInMemoryAsync(string token)
    {
        try
        {
            var sessionId = GetSessionId();
            var userId = GetUserId();
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1))
                .SetAbsoluteExpiration(TimeSpan.FromHours(2));
            
            // Store by session ID
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                _memoryCache.Set(sessionKey, token, cacheOptions);
            }
            
            // Store by user ID
            if (!string.IsNullOrEmpty(userId))
            {
                var userKey = $"{TokenKeyPrefix}user_{userId}";
                _memoryCache.Set(userKey, token, cacheOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache token in memory");
        }
    }
    
    private async Task RemoveTokenFromMemoryAsync()
    {
        try
        {
            var sessionId = GetSessionId();
            var userId = GetUserId();
            
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionKey = $"{TokenKeyPrefix}session_{sessionId}";
                _memoryCache.Remove(sessionKey);
            }
            
            if (!string.IsNullOrEmpty(userId))
            {
                var userKey = $"{TokenKeyPrefix}user_{userId}";
                _memoryCache.Remove(userKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove token from memory");
        }
    }
    
    private string? GetTokenFromSession()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session?.IsAvailable == true)
            {
                var sessionToken = httpContext.Session.GetString(TokenSessionKey);
                return sessionToken;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get token from session");
        }
        return null;
    }
    
    private void SetTokenInSession(string token)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session?.IsAvailable == true)
            {
                httpContext.Session.SetString(TokenSessionKey, token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set token in session");
        }
    }
    
    private void RemoveTokenFromSession()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session?.IsAvailable == true)
            {
                httpContext.Session.Remove(TokenSessionKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove token from session");
        }
    }
    
    private string? GetTokenFromCookie()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request?.Cookies != null)
            {
                var encryptedToken = httpContext.Request.Cookies[TokenCookieName];
                if (!string.IsNullOrEmpty(encryptedToken))
                {
                    var decryptedToken = _dataProtector.Unprotect(encryptedToken);
                    return decryptedToken;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get token from cookie (token may be corrupted or expired)");
        }
        return null;
    }
    
    private void SetTokenInCookie(string token)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Response != null)
            {
                var encryptedToken = _dataProtector.Protect(token);
                
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, // HTTPS only
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7), // 7 days expiration
                    IsEssential = true
                };
                
                httpContext.Response.Cookies.Append(TokenCookieName, encryptedToken, cookieOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set token in cookie");
        }
    }
    
    private void RemoveTokenFromCookie()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Response != null)
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(-1), // Expire the cookie
                    IsEssential = true
                };
                
                httpContext.Response.Cookies.Append(TokenCookieName, "", cookieOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove token from cookie");
        }
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
                    return userId;
                }
            }
            
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
            if (httpContext?.Session?.IsAvailable == true)
            {
                return httpContext.Session.Id;
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