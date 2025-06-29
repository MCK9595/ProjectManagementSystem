using ProjectManagementSystem.Shared.Common.Services;

namespace ProjectManagementSystem.WebApp.Services;

public class SessionTokenService : ISessionTokenService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionTokenService> _logger;
    private const string TokenKey = "authToken";

    public SessionTokenService(IHttpContextAccessor httpContextAccessor, ILogger<SessionTokenService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _logger.LogDebug("SessionTokenService initialized");
    }

    public Task<string?> GetTokenAsync()
    {
        _logger.LogDebug("Getting token from session");
        
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null)
        {
            _logger.LogWarning("HttpContext or Session is null - cannot retrieve token");
            return Task.FromResult<string?>(null);
        }

        var token = session.GetString(TokenKey);
        _logger.LogDebug("Token retrieved from session: {HasToken}", !string.IsNullOrEmpty(token));
        
        return Task.FromResult(token);
    }

    public Task SetTokenAsync(string token)
    {
        _logger.LogInformation("Setting token in session");
        
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString(TokenKey, token);
            _logger.LogDebug("Token successfully stored in session");
        }
        else
        {
            _logger.LogError("Cannot set token - HttpContext or Session is null");
        }
        
        return Task.CompletedTask;
    }

    public Task RemoveTokenAsync()
    {
        _logger.LogInformation("Removing token from session");
        
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.Remove(TokenKey);
            _logger.LogDebug("Token successfully removed from session");
        }
        else
        {
            _logger.LogWarning("Cannot remove token - HttpContext or Session is null");
        }
        
        return Task.CompletedTask;
    }
    
    public Task FlushPendingTokenAsync()
    {
        // Server-side implementation doesn't need to flush pending tokens
        _logger.LogDebug("FlushPendingTokenAsync called on server-side - no action needed");
        return Task.CompletedTask;
    }
}