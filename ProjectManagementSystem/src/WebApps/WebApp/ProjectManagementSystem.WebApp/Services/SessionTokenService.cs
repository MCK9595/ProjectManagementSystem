using ProjectManagementSystem.Shared.Common.Services;
using System.Collections.Concurrent;

namespace ProjectManagementSystem.WebApp.Services;

public class SessionTokenService : ISessionTokenService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionTokenService> _logger;
    private const string TokenKey = "authToken";
    
    // Pending token storage for handling session timing issues
    // Dictionary to store pending tokens per session ID for multi-user support
    private readonly ConcurrentDictionary<string, string> _pendingTokens = new();

    public SessionTokenService(IHttpContextAccessor httpContextAccessor, ILogger<SessionTokenService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _logger.LogDebug("SessionTokenService initialized");
    }

    public Task<string?> GetTokenAsync()
    {
        _logger.LogDebug("Getting token from session");
        
        // First check if we have a pending token for this session
        var httpContext = _httpContextAccessor.HttpContext;
        _logger.LogDebug("GetToken - HttpContext is null: {IsNull}", httpContext == null);
        
        if (httpContext?.Session != null)
        {
            var sessionId = httpContext.Session.Id;
            if (_pendingTokens.TryGetValue(sessionId, out var pendingToken) && !string.IsNullOrEmpty(pendingToken))
            {
                _logger.LogDebug("Token retrieved from pending storage for session {SessionId}: Length: {Length}", sessionId, pendingToken.Length);
                return Task.FromResult<string?>(pendingToken);
            }
        }
        
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null - cannot retrieve token");
            return Task.FromResult<string?>(null);
        }
        
        var session = httpContext.Session;
        _logger.LogDebug("GetToken - Session is null: {IsNull}", session == null);
        
        if (session == null)
        {
            _logger.LogWarning("Session is null - cannot retrieve token");
            return Task.FromResult<string?>(null);
        }

        _logger.LogDebug("GetToken - Session ID: {SessionId}", session.Id);
        
        try
        {
            var token = session.GetString(TokenKey);
            _logger.LogDebug("Token retrieved from session: {HasToken}, Length: {Length}", 
                !string.IsNullOrEmpty(token), token?.Length ?? 0);
            
            return Task.FromResult(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while retrieving token from session");
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetTokenAsync(string token)
    {
        _logger.LogInformation("Setting token in storage");
        
        var httpContext = _httpContextAccessor.HttpContext;
        _logger.LogDebug("HttpContext is null: {IsNull}", httpContext == null);
        
        if (httpContext?.Session == null)
        {
            _logger.LogWarning("Cannot store token - HttpContext or Session is null");
            return Task.CompletedTask;
        }

        var session = httpContext.Session;
        var sessionId = session.Id;
        
        // Always store in pending storage first as a backup
        _pendingTokens[sessionId] = token;
        _logger.LogDebug("Token stored in pending storage for session {SessionId}: Length: {Length}", sessionId, token.Length);
        
        // Try to store directly in session
        try
        {
            // Ensure session is loaded before attempting to write
            if (!session.IsAvailable)
            {
                _logger.LogDebug("Session not available, attempting to load");
                session.LoadAsync().GetAwaiter().GetResult();
            }
            
            session.SetString(TokenKey, token);
            _logger.LogInformation("Token successfully stored in session with ID: {SessionId}", sessionId);
            
            // Verify the token was stored
            var storedToken = session.GetString(TokenKey);
            if (!string.IsNullOrEmpty(storedToken) && storedToken == token)
            {
                _logger.LogDebug("Token verification successful - removing from pending storage");
                _pendingTokens.TryRemove(sessionId, out _);
            }
            else
            {
                _logger.LogWarning("Token verification failed - keeping in pending storage");
            }
        }
        catch (InvalidOperationException sessionEx) when (sessionEx.Message.Contains("session cannot be established") || sessionEx.Message.Contains("response has started"))
        {
            _logger.LogInformation("Session cannot be established after response started - token will remain in pending storage for later flush");
            _logger.LogDebug("Session timing exception: {Message}", sessionEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while storing token in session - keeping in pending storage");
        }
        
        return Task.CompletedTask;
    }

    public Task RemoveTokenAsync()
    {
        _logger.LogInformation("Removing token from storage");
        
        // Clear pending token first
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Session != null)
        {
            var sessionId = httpContext.Session.Id;
            if (_pendingTokens.TryRemove(sessionId, out _))
            {
                _logger.LogDebug("Pending token cleared for session {SessionId}", sessionId);
            }
        }
        
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            try
            {
                session.Remove(TokenKey);
                _logger.LogDebug("Token successfully removed from session");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while removing token from session");
            }
        }
        else
        {
            _logger.LogWarning("Cannot remove token from session - HttpContext or Session is null");
        }
        
        return Task.CompletedTask;
    }
    
    public Task FlushPendingTokenAsync()
    {
        _logger.LogDebug("FlushPendingTokenAsync called");
        
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Session == null)
        {
            _logger.LogDebug("Cannot flush pending token - session not available");
            return Task.CompletedTask;
        }
        
        var session = httpContext.Session;
        var sessionId = session.Id;
        
        if (!_pendingTokens.TryGetValue(sessionId, out var pendingToken) || string.IsNullOrEmpty(pendingToken))
        {
            _logger.LogDebug("No pending token to flush for session {SessionId}", sessionId);
            return Task.CompletedTask;
        }
        
        // Check if token is already in session
        try
        {
            var existingToken = session.GetString(TokenKey);
            if (!string.IsNullOrEmpty(existingToken) && existingToken == pendingToken)
            {
                _logger.LogDebug("Token already exists in session, removing from pending storage");
                _pendingTokens.TryRemove(sessionId, out _);
                return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check existing token in session {SessionId}", sessionId);
        }
        
        // Try to flush pending token to session
        try
        {
            // Ensure session is available for writing
            if (!session.IsAvailable)
            {
                _logger.LogDebug("Session not available for writing, loading session first");
                session.LoadAsync().GetAwaiter().GetResult();
            }
            
            session.SetString(TokenKey, pendingToken);
            _logger.LogInformation("Pending token successfully flushed to session {SessionId}", sessionId);
            
            // Verify the token was stored correctly
            var storedToken = session.GetString(TokenKey);
            if (!string.IsNullOrEmpty(storedToken) && storedToken == pendingToken)
            {
                _logger.LogDebug("Token flush verification successful - removing from pending storage");
                _pendingTokens.TryRemove(sessionId, out _);
            }
            else
            {
                _logger.LogWarning("Token flush verification failed for session {SessionId}", sessionId);
            }
        }
        catch (InvalidOperationException sessionEx) when (sessionEx.Message.Contains("session cannot be established") || sessionEx.Message.Contains("response has started"))
        {
            _logger.LogDebug("Session cannot be established during flush for session {SessionId} - will retry on next request", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush pending token to session {SessionId} - will retry on next request", sessionId);
        }
        
        return Task.CompletedTask;
    }
}