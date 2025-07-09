using Microsoft.Extensions.Logging;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectManagementSystem.WebApp.Services;

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
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginDto);
            var requestDuration = DateTime.UtcNow - requestStartTime;
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Login request completed - Status: {StatusCode}, Duration: {Duration}ms", 
                    response.StatusCode, requestDuration.TotalMilliseconds);
            }
            
            var jsonContent = await response.Content.ReadAsStringAsync();
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
        catch (TaskCanceledException tcEx)
        {
            _lastError = "Login request was cancelled. Please try again.";
            _logger.LogError(tcEx, "Login request cancelled for user: {Username}", loginDto.Username);
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
            await _httpClient.PostAsync("/api/auth/logout", null);
        }
        catch (Exception)
        {
            // Log exception
        }
        finally
        {
            await RemoveTokenAsync();
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
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

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Getting current user from API - BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            }
            
            // Set Authorization header for this request
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            
            var requestStartTime = DateTime.UtcNow;
            var response = await _httpClient.GetAsync("/api/auth/me");
            var requestDuration = DateTime.UtcNow - requestStartTime;
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("GetCurrentUser request completed - Status: {StatusCode}, Duration: {Duration}ms", 
                    response.StatusCode, requestDuration.TotalMilliseconds);
            }
            
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
            var token = await _sessionTokenService.GetTokenAsync();
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Token retrieved from storage - HasToken: {HasToken}", !string.IsNullOrEmpty(token));
            }
            
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting token");
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            await _sessionTokenService.SetTokenAsync(token);
            _logger.LogDebug("Token stored successfully");
            
            // Try to flush pending token to session immediately
            await _sessionTokenService.FlushPendingTokenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception setting token");
        }
    }

    public async Task RemoveTokenAsync()
    {
        try
        {
            await _sessionTokenService.RemoveTokenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception removing token");
        }
    }

    public string? GetLastError()
    {
        return _lastError;
    }

}