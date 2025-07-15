using Microsoft.Extensions.Logging;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Client.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectManagementSystem.Shared.Client.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<UserDto?> GetCurrentUserAsync();
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
    string? GetLastError();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly ILogger<AuthService> _logger;
    private string? _lastError;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ISessionTokenService sessionTokenService,
        ILogger<AuthService> logger)
    {
        _logger = logger;
        try
        {
            _httpClient = httpClientFactory.CreateClient("ApiGateway");
            _sessionTokenService = sessionTokenService;
            
            _logger.LogDebug("AuthService initialized - BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AuthService");
            throw;
        }
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
    {
        try
        {
            _logger.LogInformation("Login attempt for user: {Username}", loginDto.Username);
            _lastError = null;
            
            var requestStartTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsJsonAsync("/api/identity/login", loginDto);
            var requestDuration = DateTime.UtcNow - requestStartTime;
            
            _logger.LogDebug("Login request completed - Status: {StatusCode}, Duration: {Duration}ms", 
                response.StatusCode, requestDuration.TotalMilliseconds);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Login failed for user: {Username} - Status: {StatusCode}, Response: {Response}", 
                    loginDto.Username, response.StatusCode, errorContent);
                
                // Try to parse error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(errorContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    _lastError = errorResponse?.Message ?? "Login failed";
                }
                catch
                {
                    _lastError = "Login failed";
                }
                
                return null;
            }
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Login response content: {JsonContent}", jsonContent);
            
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse?.Success == true && apiResponse.Data?.Token != null)
            {
                _logger.LogInformation("Login successful for user: {Username}", loginDto.Username);
                await SetTokenAsync(apiResponse.Data.Token);
                return apiResponse.Data;
            }
            else
            {
                _lastError = apiResponse?.Message ?? "An error occurred during login";
                _logger.LogWarning("Login failed for user: {Username} - {Message}", 
                    loginDto.Username, apiResponse?.Message);
                return null;
            }
        }
        catch (HttpRequestException httpEx)
        {
            _lastError = "Network error occurred during login. Please check your connection.";
            _logger.LogError(httpEx, "Network error during login for user: {Username}", loginDto.Username);
            return null;
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            _lastError = "Login request timed out. Please try again.";
            _logger.LogError(tcEx, "Login request timeout for user: {Username}", loginDto.Username);
            return null;
        }
        catch (JsonException jsonEx)
        {
            _lastError = "Invalid response from server. Please try again.";
            _logger.LogError(jsonEx, "JSON deserialization error during login for user: {Username}", loginDto.Username);
            return null;
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred during login. Please try again.";
            _logger.LogError(ex, "Login failed for user: {Username}", loginDto.Username);
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var token = await GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                await _httpClient.PostAsync("/api/identity/logout", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during logout request");
            // Continue with local logout even if server request fails
        }
        finally
        {
            await RemoveTokenAsync();
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            _logger.LogDebug("Checking authentication status");
            
            var token = await GetTokenAsync();
            var isAuthenticated = !string.IsNullOrEmpty(token);
            
            _logger.LogDebug("Authentication check result: {IsAuthenticated}", isAuthenticated);
            
            if (isAuthenticated)
            {
                // Also verify we can get current user info
                try
                {
                    var user = await GetCurrentUserAsync();
                    var userDataAvailable = user != null;
                    
                    _logger.LogDebug("User data availability: {UserDataAvailable}", userDataAvailable);
                    
                    if (!userDataAvailable)
                    {
                        _logger.LogWarning("Token exists but user data not available - possible token expiry");
                        await RemoveTokenAsync(); // Clean up invalid token
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to verify user data - token may be invalid");
                    await RemoveTokenAsync(); // Clean up invalid token
                    return false;
                }
            }
            
            return isAuthenticated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception checking authentication status");
            return false;
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("No token found, user not authenticated");
                return null;
            }

            _logger.LogDebug("Getting current user from API - BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            
            var requestStartTime = DateTime.UtcNow;
            var response = await _httpClient.GetAsync("/api/identity/me");
            var requestDuration = DateTime.UtcNow - requestStartTime;
            
            _logger.LogDebug("GetCurrentUser request completed - Status: {StatusCode}, Duration: {Duration}ms", 
                response.StatusCode, requestDuration.TotalMilliseconds);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                // Parse the ApiResponse wrapper to extract the UserDto data
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogDebug("Current user retrieved: {Username}", apiResponse.Data.Username);
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve current user - API response unsuccessful");
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("GetCurrentUser API call failed with status: {StatusCode}", response.StatusCode);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token is invalid, remove it
                    await RemoveTokenAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetCurrentUserAsync");
        }

        return null;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            _logger.LogDebug("Getting token from session token service");
            var token = await _sessionTokenService.GetTokenAsync();
            
            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("Token retrieved successfully - Length: {Length}", token.Length);
                
                // Basic token validation
                var parts = token.Split('.');
                if (parts.Length == 3)
                {
                    _logger.LogDebug("Token format appears valid (3 parts)");
                }
                else
                {
                    _logger.LogWarning("Token format invalid - expected 3 parts, got {Parts}", parts.Length);
                }
            }
            else
            {
                _logger.LogDebug("No token found in storage");
            }
            
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting token from storage");
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Attempted to set null or empty token");
                return;
            }
            
            _logger.LogInformation("Setting authentication token - Length: {Length}", token.Length);
            
            await _sessionTokenService.SetTokenAsync(token);
            _logger.LogDebug("Token stored in session service successfully");
            
            // Try to flush pending token to session immediately
            await _sessionTokenService.FlushPendingTokenAsync();
            _logger.LogDebug("Token flushed to session storage");
            
            // Verify token was stored correctly
            var verifyToken = await _sessionTokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(verifyToken) && verifyToken == token)
            {
                _logger.LogDebug("Token storage verification successful");
            }
            else
            {
                _logger.LogWarning("Token storage verification failed - stored token differs from original");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception setting token in storage");
        }
    }

    public async Task RemoveTokenAsync()
    {
        try
        {
            _logger.LogInformation("Removing authentication token from storage");
            
            await _sessionTokenService.RemoveTokenAsync();
            
            // Verify token was removed
            var verifyToken = await _sessionTokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(verifyToken))
            {
                _logger.LogDebug("Token removal verification successful");
            }
            else
            {
                _logger.LogWarning("Token removal verification failed - token still present");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception removing token from storage");
        }
    }

    public string? GetLastError()
    {
        return _lastError;
    }
}