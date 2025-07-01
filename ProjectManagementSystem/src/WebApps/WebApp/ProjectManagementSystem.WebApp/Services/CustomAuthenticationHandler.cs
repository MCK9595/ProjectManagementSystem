using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using ProjectManagementSystem.Shared.Common.Services;

namespace ProjectManagementSystem.WebApp.Services;

public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ISessionTokenService _sessionTokenService;
    private readonly ILogger<CustomAuthenticationHandler> _logger;

    public CustomAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessionTokenService sessionTokenService)
        : base(options, logger, encoder)
    {
        _sessionTokenService = sessionTokenService;
        _logger = logger.CreateLogger<CustomAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            var token = await _sessionTokenService.GetTokenAsync();
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No token found in session");
                return AuthenticateResult.NoResult();
            }

            // Parse JWT token to extract claims
            var claims = ParseJwtClaims(token);
            if (claims == null || !claims.Any())
            {
                _logger.LogWarning("Invalid or expired JWT token");
                await _sessionTokenService.RemoveTokenAsync();
                return AuthenticateResult.Fail("Invalid token");
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            _logger.LogDebug("Authentication successful for user: {UserId}", 
                claims.FirstOrDefault(c => c.Type == "id")?.Value);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            return AuthenticateResult.Fail(ex.Message);
        }
    }

    private List<Claim>? ParseJwtClaims(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            var payload = parts[1];
            // Add padding if necessary
            var paddingNeeded = 4 - (payload.Length % 4);
            if (paddingNeeded < 4)
                payload += new string('=', paddingNeeded);

            var jsonBytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            
            using var document = JsonDocument.Parse(json);
            var claims = new List<Claim>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var claimType = property.Name switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "email" => ClaimTypes.Email,
                    "name" => ClaimTypes.Name,
                    "role" => ClaimTypes.Role,
                    "exp" => "exp",
                    "iat" => "iat",
                    "iss" => "iss",
                    "aud" => "aud",
                    _ => property.Name
                };

                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? "",
                    JsonValueKind.Number => property.Value.GetInt64().ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => property.Value.ToString()
                };

                if (!string.IsNullOrEmpty(value))
                {
                    claims.Add(new Claim(claimType, value));
                }
            }

            // Check if token is expired
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
            {
                var expDateTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expDateTime <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("JWT token has expired");
                    return null;
                }
            }

            return claims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JWT token");
            return null;
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Redirect to login page for unauthenticated requests
        Context.Response.Redirect("/login");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        // Redirect to access denied page for forbidden requests
        Context.Response.Redirect("/access-denied");
        return Task.CompletedTask;
    }
}