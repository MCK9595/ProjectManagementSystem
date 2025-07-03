using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IntegrationTests.Helpers;

/// <summary>
/// Helper class for authentication-related operations in integration tests
/// </summary>
public class AuthenticationHelper
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthenticationHelper(HttpClient httpClient, 
        string secretKey = "ThisIsAVeryLongSecretKeyForJWTTokenGenerationInTests123456789",
        string issuer = "TestIssuer",
        string audience = "TestAudience")
    {
        _httpClient = httpClient;
        _secretKey = secretKey;
        _issuer = issuer;
        _audience = audience;
    }

    /// <summary>
    /// Login with username and password, returns authentication response
    /// </summary>
    public async Task<AuthResponseDto?> LoginAsync(string username, string password)
    {
        var loginRequest = new LoginDto
        {
            Username = username,
            Password = password
        };

        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<AuthResponseDto>>(content);
        
        return apiResponse?.Data;
    }

    /// <summary>
    /// Login with default admin credentials
    /// </summary>
    public async Task<AuthResponseDto?> LoginAsAdminAsync()
    {
        return await LoginAsync("admin@projectmanagement.com", "AdminPassword123!");
    }

    /// <summary>
    /// Set Bearer token authentication header
    /// </summary>
    public void SetBearerToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Clear authentication header
    /// </summary>
    public void ClearAuthentication()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Get current user information using JWT token
    /// </summary>
    public async Task<UserDto?> GetCurrentUserAsync()
    {
        var response = await _httpClient.GetAsync("/api/auth/me");
        
        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<UserDto>>(content);
        
        return apiResponse?.Data;
    }

    /// <summary>
    /// Refresh JWT token using refresh token
    /// </summary>
    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var refreshRequest = new { RefreshToken = refreshToken };
        
        var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        
        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<AuthResponseDto>>(content);
        
        return apiResponse?.Data;
    }

    /// <summary>
    /// Logout using refresh token
    /// </summary>
    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var logoutRequest = new { RefreshToken = refreshToken };
        
        var response = await _httpClient.PostAsJsonAsync("/api/auth/logout", logoutRequest);
        
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Validate current JWT token
    /// </summary>
    public async Task<bool> ValidateTokenAsync()
    {
        var response = await _httpClient.PostAsync("/api/auth/validate", null);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Extract claims from JWT token
    /// </summary>
    public ClaimsPrincipal? ExtractClaimsFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if JWT token is expired
    /// </summary>
    public bool IsTokenExpired(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);
            
            return jsonToken.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Generate an invalid JWT token for testing purposes
    /// </summary>
    public string GenerateInvalidToken()
    {
        return "invalid.jwt.token";
    }

    /// <summary>
    /// Generate an expired JWT token for testing purposes
    /// </summary>
    public string GenerateExpiredToken(int userId, string username, string email, string role)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, 
                new DateTimeOffset(DateTime.UtcNow.AddDays(-1)).ToUnixTimeSeconds().ToString(), 
                ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Generate a tampered JWT token for testing purposes
    /// </summary>
    public string GenerateTamperedToken(string validToken)
    {
        // Simple tampering - just change the last character
        return validToken.Length > 0 ? validToken[..^1] + "X" : "tampered";
    }
}