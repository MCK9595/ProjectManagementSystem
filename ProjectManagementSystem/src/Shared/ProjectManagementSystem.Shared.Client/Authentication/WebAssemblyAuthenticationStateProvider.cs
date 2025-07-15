using Microsoft.Extensions.Logging;
using ProjectManagementSystem.Shared.Client.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProjectManagementSystem.Shared.Client.Authentication;

public class WebAssemblyAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ISessionTokenService _tokenService;
    private readonly ILogger<WebAssemblyAuthenticationStateProvider> _logger;
    private readonly JwtSecurityTokenHandler _jwtHandler;

    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public WebAssemblyAuthenticationStateProvider(
        ISessionTokenService tokenService,
        ILogger<WebAssemblyAuthenticationStateProvider> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
        _jwtHandler = new JwtSecurityTokenHandler();
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _tokenService.GetTokenAsync();
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No token found, returning anonymous user");
                return CreateAnonymousState();
            }

            var claimsPrincipal = GetClaimsPrincipalFromToken(token);
            
            if (claimsPrincipal == null)
            {
                _logger.LogWarning("Failed to parse token, returning anonymous user");
                return CreateAnonymousState();
            }

            // Check if token is expired
            var expiryClaim = claimsPrincipal.FindFirst("exp");
            if (expiryClaim != null && long.TryParse(expiryClaim.Value, out var exp))
            {
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expiryTime <= DateTimeOffset.UtcNow)
                {
                    _logger.LogInformation("Token is expired, removing and returning anonymous user");
                    await _tokenService.RemoveTokenAsync();
                    return CreateAnonymousState();
                }
            }

            _currentUser = claimsPrincipal;
            _logger.LogDebug("User authenticated as {Username}", claimsPrincipal.Identity?.Name);
            
            return new AuthenticationState(claimsPrincipal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authentication state");
            return CreateAnonymousState();
        }
    }

    public async Task MarkUserAsAuthenticatedAsync(string token)
    {
        try
        {
            await _tokenService.SetTokenAsync(token);
            
            var claimsPrincipal = GetClaimsPrincipalFromToken(token);
            if (claimsPrincipal != null)
            {
                _currentUser = claimsPrincipal;
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
                _logger.LogInformation("User marked as authenticated: {Username}", claimsPrincipal.Identity?.Name);
            }
            else
            {
                _logger.LogWarning("Failed to parse token when marking user as authenticated");
                await MarkUserAsLoggedOutAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking user as authenticated");
            await MarkUserAsLoggedOutAsync();
        }
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        try
        {
            await _tokenService.RemoveTokenAsync();
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(CreateAnonymousState()));
            _logger.LogInformation("User logged out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking user as logged out");
        }
    }

    private ClaimsPrincipal? GetClaimsPrincipalFromToken(string token)
    {
        try
        {
            if (!_jwtHandler.CanReadToken(token))
            {
                _logger.LogWarning("Token is not a valid JWT");
                return null;
            }

            var jwtToken = _jwtHandler.ReadJwtToken(token);
            
            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
            
            // Add standard claims if they don't exist
            if (identity.FindFirst(ClaimTypes.Name) == null && identity.FindFirst("name") != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, identity.FindFirst("name")!.Value));
            }
            
            if (identity.FindFirst(ClaimTypes.Email) == null && identity.FindFirst("email") != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, identity.FindFirst("email")!.Value));
            }
            
            if (identity.FindFirst(ClaimTypes.NameIdentifier) == null && identity.FindFirst("sub") != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, identity.FindFirst("sub")!.Value));
            }

            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JWT token");
            return null;
        }
    }

    private static AuthenticationState CreateAnonymousState()
    {
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}