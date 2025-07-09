using ProjectManagementSystem.Shared.Common.Services;
using System.Net.Http.Headers;

namespace ProjectManagementSystem.WebApp.Services;

public class TokenHandler : DelegatingHandler
{
    private readonly ISessionTokenService _sessionTokenService;
    private readonly ILogger<TokenHandler> _logger;

    public TokenHandler(ISessionTokenService sessionTokenService, ILogger<TokenHandler> logger)
    {
        _sessionTokenService = sessionTokenService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("TokenHandler: Processing request to {Uri}", request.RequestUri);
            
            var accessToken = await _sessionTokenService.GetTokenAsync();
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger.LogInformation("TokenHandler: Adding JWT token to Authorization header (Length: {Length})", accessToken.Length);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _logger.LogDebug("TokenHandler: Authorization header set: {AuthHeader}", request.Headers.Authorization?.ToString() ?? "NULL");
            }
            else
            {
                _logger.LogWarning("TokenHandler: No access token found in session");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TokenHandler: Error getting access token");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}