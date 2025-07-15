using Microsoft.Extensions.Logging;

namespace ProjectManagementSystem.Shared.Client.Services;

/// <summary>
/// Server-side implementation of ISessionTokenService for pre-rendering
/// This service doesn't store tokens as it runs on the server side
/// </summary>
public class ServerSideTokenService : ISessionTokenService
{
    private readonly ILogger<ServerSideTokenService> _logger;

    public ServerSideTokenService(ILogger<ServerSideTokenService> logger)
    {
        _logger = logger;
    }

    public Task<string?> GetTokenAsync()
    {
        _logger.LogDebug("ServerSideTokenService: GetTokenAsync called - returning null (server-side pre-rendering)");
        return Task.FromResult<string?>(null);
    }

    public Task SetTokenAsync(string token)
    {
        _logger.LogDebug("ServerSideTokenService: SetTokenAsync called - ignoring token storage (server-side pre-rendering)");
        return Task.CompletedTask;
    }

    public Task RemoveTokenAsync()
    {
        _logger.LogDebug("ServerSideTokenService: RemoveTokenAsync called - no action needed (server-side pre-rendering)");
        return Task.CompletedTask;
    }

    public Task FlushPendingTokenAsync()
    {
        _logger.LogDebug("ServerSideTokenService: FlushPendingTokenAsync called - no action needed (server-side pre-rendering)");
        return Task.CompletedTask;
    }
}