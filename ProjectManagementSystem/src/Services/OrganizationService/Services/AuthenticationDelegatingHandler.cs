using System.Net.Http.Headers;

namespace ProjectManagementSystem.OrganizationService.Services;

public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthenticationDelegatingHandler> _logger;

    public AuthenticationDelegatingHandler(IHttpContextAccessor httpContextAccessor, ILogger<AuthenticationDelegatingHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null)
        {
            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _logger.LogInformation("Adding Authorization header to service-to-service request: {RequestUri}", request.RequestUri);
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
            }
            else
            {
                _logger.LogWarning("No Authorization header found in current request context for: {RequestUri}", request.RequestUri);
            }
        }
        else
        {
            _logger.LogWarning("No HttpContext available for service-to-service request: {RequestUri}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}