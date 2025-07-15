using Microsoft.Extensions.Logging;
using ProjectManagementSystem.Shared.Client.Services;

namespace ProjectManagementSystem.Shared.Client.Handlers;

public class TokenHandler : DelegatingHandler
{
    private readonly ISessionTokenService _tokenService;
    private readonly ILogger<TokenHandler> _logger;

    public TokenHandler(ISessionTokenService tokenService, ILogger<TokenHandler> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            // Try to flush any pending tokens first
            await _tokenService.FlushPendingTokenAsync();
            
            // Get token from storage
            var token = await _tokenService.GetTokenAsync();
            
            if (!string.IsNullOrEmpty(token))
            {
                // Add Authorization header if not already present
                if (!request.Headers.Contains("Authorization"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    _logger.LogDebug("Added Authorization header to request to {Uri}", request.RequestUri);
                }
            }
            else
            {
                _logger.LogDebug("No token available for request to {Uri}", request.RequestUri);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the request if token handling fails
            _logger.LogWarning(ex, "Failed to add token to request to {Uri}", request.RequestUri);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Handle 401 Unauthorized responses
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogInformation("Received 401 Unauthorized response from {Uri}, removing stored token", request.RequestUri);
            
            try
            {
                // Remove the token as it might be expired or invalid
                await _tokenService.RemoveTokenAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove token after 401 response");
            }
        }

        return response;
    }
}