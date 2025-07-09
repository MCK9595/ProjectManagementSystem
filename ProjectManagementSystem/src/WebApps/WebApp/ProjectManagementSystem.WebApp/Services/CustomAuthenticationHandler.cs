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
        _logger.LogDebug("Starting authentication check");
        
        try
        {
            var token = await _sessionTokenService.GetTokenAsync();
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No token found - user not authenticated");
                return AuthenticateResult.NoResult();
            }
            
            _logger.LogDebug("Token found, validating JWT token");

            // Parse JWT token to extract claims
            var claims = ParseJwtClaims(token);
            if (claims == null || !claims.Any())
            {
                _logger.LogWarning("Invalid or expired JWT token - removing from storage");
                await _sessionTokenService.RemoveTokenAsync();
                return AuthenticateResult.Fail("Invalid or expired token");
            }

            var userId = claims.FirstOrDefault(c => c.Type == "id" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            var username = claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value;
            var email = claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
            
            _logger.LogDebug("JWT token validation successful - UserId: {UserId}, Username: {Username}, Email: {Email}", 
                userId ?? "null", username ?? "null", email ?? "null");

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            _logger.LogInformation("Authentication successful for user: {Username} (ID: {UserId})", 
                username ?? "Unknown", userId ?? "Unknown");

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during authentication process");
            
            // Try to remove potentially corrupted token
            try
            {
                await _sessionTokenService.RemoveTokenAsync();
            }
            catch (Exception removeEx)
            {
                _logger.LogWarning(removeEx, "Failed to remove token after authentication error");
            }
            
            return AuthenticateResult.Fail($"Authentication error: {ex.Message}");
        }
    }

    private List<Claim>? ParseJwtClaims(string token)
    {
        try
        {
            _logger.LogDebug("Parsing JWT token");
            
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("Invalid JWT token format - expected 3 parts, got {Parts}", parts.Length);
                return null;
            }

            var payload = parts[1];
            
            // Add padding if necessary
            var paddingNeeded = 4 - (payload.Length % 4);
            if (paddingNeeded < 4)
                payload += new string('=', paddingNeeded);

            var jsonBytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            
            _logger.LogDebug("JWT payload decoded successfully");
            
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
                    "id" => "id", // Keep custom id claim
                    "username" => "username", // Keep custom username claim
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

            _logger.LogDebug("Extracted {ClaimCount} claims from JWT token", claims.Count);

            // Check if token is expired
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
            {
                var expDateTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                var timeUntilExpiry = expDateTime - DateTimeOffset.UtcNow;
                
                if (expDateTime <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("JWT token has expired at {ExpiryTime}", expDateTime);
                    return null;
                }
                
                _logger.LogDebug("JWT token expires at {ExpiryTime} (in {Minutes} minutes)", 
                    expDateTime, timeUntilExpiry.TotalMinutes);
            }
            else
            {
                _logger.LogWarning("JWT token has no expiration claim");
            }

            return claims;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid JWT token format");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in JWT token payload");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing JWT token");
            return null;
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var requestPath = Context.Request.Path;
        _logger.LogInformation("Authentication challenge for path: {RequestPath}", requestPath);
        
        // Redirect to login page for unauthenticated requests
        var returnUrl = Context.Request.Path + Context.Request.QueryString;
        var loginUrl = $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        
        _logger.LogDebug("Redirecting to login page with return URL: {ReturnUrl}", returnUrl);
        Context.Response.Redirect(loginUrl);
        
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        var requestPath = Context.Request.Path;
        var username = Context.User?.Identity?.Name ?? "Unknown";
        
        _logger.LogWarning("Access forbidden for user {Username} to path: {RequestPath}", username, requestPath);
        
        // Redirect to access denied page for forbidden requests
        Context.Response.Redirect("/access-denied");
        return Task.CompletedTask;
    }
}