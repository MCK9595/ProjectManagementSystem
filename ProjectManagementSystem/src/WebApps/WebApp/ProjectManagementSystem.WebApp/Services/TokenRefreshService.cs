using ProjectManagementSystem.Shared.Common.Services;
using System.Text.Json;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.WebApp.Services;

public class TokenRefreshService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenRefreshService> _logger;
    private Timer? _timer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes
    private readonly TimeSpan _refreshThreshold = TimeSpan.FromMinutes(10); // Refresh if token expires in 10 minutes

    public TokenRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<TokenRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token Refresh Service started");
        
        _timer = new Timer(CheckAndRefreshTokens, null, TimeSpan.Zero, _checkInterval);
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token Refresh Service stopped");
        
        _timer?.Change(Timeout.Infinite, 0);
        
        return Task.CompletedTask;
    }

    private async void CheckAndRefreshTokens(object? state)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionTokenService = scope.ServiceProvider.GetRequiredService<ISessionTokenService>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            
            var currentToken = await sessionTokenService.GetTokenAsync();
            
            if (string.IsNullOrEmpty(currentToken))
            {
                _logger.LogDebug("No token found, skipping refresh check");
                return;
            }
            
            // Parse JWT token to check expiration
            if (IsTokenNearExpiration(currentToken))
            {
                _logger.LogInformation("Token is near expiration, attempting refresh");
                
                var refreshed = await RefreshTokenAsync(sessionTokenService, httpClientFactory);
                
                if (refreshed)
                {
                    _logger.LogInformation("Token refreshed successfully");
                }
                else
                {
                    _logger.LogWarning("Token refresh failed");
                }
            }
            else
            {
                _logger.LogDebug("Token is still valid, no refresh needed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh check");
        }
    }

    private bool IsTokenNearExpiration(string token)
    {
        try
        {
            // Simple JWT parsing to check expiration
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return true; // Invalid token format, consider it expired
            }
            
            var payload = parts[1];
            
            // Add padding if needed for Base64 decoding
            switch (payload.Length % 4)
            {
                case 2:
                    payload += "==";
                    break;
                case 3:
                    payload += "=";
                    break;
            }
            
            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);
            
            using var document = JsonDocument.Parse(payloadJson);
            
            if (document.RootElement.TryGetProperty("exp", out var expElement))
            {
                var exp = expElement.GetInt64();
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                var timeUntilExpiration = expirationTime - DateTimeOffset.UtcNow;
                
                _logger.LogDebug("Token expires in {TimeUntilExpiration}, threshold is {RefreshThreshold}", 
                    timeUntilExpiration, _refreshThreshold);
                
                return timeUntilExpiration <= _refreshThreshold;
            }
            
            return true; // No expiration claim, consider it expired
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JWT token expiration");
            return true; // If we can't parse it, consider it expired
        }
    }

    private async Task<bool> RefreshTokenAsync(ISessionTokenService sessionTokenService, IHttpClientFactory httpClientFactory)
    {
        try
        {
            // Note: Since we don't store refresh tokens separately in the current implementation,
            // we'll implement a simpler approach by trying to re-authenticate using the current session
            
            // For now, we'll just log that refresh is needed and return false
            // In a full implementation, you would:
            // 1. Store refresh tokens separately from access tokens
            // 2. Call the refresh endpoint with the refresh token
            // 3. Update the stored access token
            
            _logger.LogInformation("Token refresh needed but refresh token not available in current implementation");
            _logger.LogInformation("Consider implementing proper refresh token storage and refresh endpoint calls");
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return false;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}

public static class TokenRefreshServiceExtensions
{
    public static IServiceCollection AddTokenRefreshService(this IServiceCollection services)
    {
        services.AddHostedService<TokenRefreshService>();
        return services;
    }
}