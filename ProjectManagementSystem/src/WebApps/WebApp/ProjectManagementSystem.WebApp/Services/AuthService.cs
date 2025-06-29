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
        _logger.LogInformation("=== AUTHSERVICE CONSTRUCTOR STARTED ===");
        
        try
        {
            _httpClient = httpClientFactory.CreateClient("ApiGateway");
            _logger.LogInformation("HttpClient created with name: ApiGateway");
            _logger.LogInformation("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress?.ToString() ?? "NULL");
            _logger.LogInformation("HttpClient Timeout: {Timeout}", _httpClient.Timeout);
            
            // Log all default headers
            foreach (var header in _httpClient.DefaultRequestHeaders)
            {
                _logger.LogInformation("Default Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create HttpClient");
            throw;
        }
        
        _sessionTokenService = sessionTokenService;
        _logger.LogInformation("SessionTokenService initialized: {ServiceType}", _sessionTokenService.GetType().Name);
        
        _logger.LogInformation("=== AUTHSERVICE CONSTRUCTOR COMPLETED ===");
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
    {
        try
        {
            _logger.LogInformation("=== LOGIN PROCESS STARTED ===");
            _logger.LogInformation("Starting login process for user: {Username}", loginDto.Username);
            _logger.LogInformation("WebAssembly client starting login process");
            _logger.LogInformation("SessionTokenService null: {IsNull}", _sessionTokenService == null);
            _lastError = null;
            
            // Log HttpClient details
            _logger.LogInformation("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogInformation("HttpClient Timeout: {Timeout}", _httpClient.Timeout);
            _logger.LogInformation("HttpClient Default Headers: {Headers}", string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}: {string.Join(", ", h.Value.ToArray())}")));
            
            // Log the full request URL
            var requestUri = new Uri(_httpClient.BaseAddress!, "/api/auth/login");
            _logger.LogInformation("Full request URI: {RequestUri}", requestUri);
            
            _logger.LogInformation("Sending HTTP POST request to /api/auth/login");
            
            // Log request details for debugging
            _logger.LogInformation("DEBUG: Making request to {RequestUri} at {Time}", requestUri, DateTime.UtcNow.ToString("HH:mm:ss"));
            
            var requestStartTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginDto);
            var requestDuration = DateTime.UtcNow - requestStartTime;
            
            _logger.LogInformation("Login request completed with status: {StatusCode} in {Duration}ms", response.StatusCode, requestDuration.TotalMilliseconds);
            _logger.LogInformation("Response Headers: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value.ToArray())}")));
            
            // Log response details for debugging
            _logger.LogInformation("DEBUG: Response received - Status: {StatusCode}, Duration: {Duration}ms", response.StatusCode, requestDuration.TotalMilliseconds);
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Raw response content: {JsonContent}", jsonContent);
            
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<AuthResponseDto>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Deserialized response - Success: {Success}, Message: {Message}, HasData: {HasData}", 
                apiResponse?.Success, apiResponse?.Message, apiResponse?.Data != null);

            if (apiResponse?.Success == true && apiResponse.Data?.Token != null)
            {
                _logger.LogInformation("Login successful, setting token");
                await SetTokenAsync(apiResponse.Data.Token);
                _logger.LogInformation("Token set successfully");
                _logger.LogInformation("=== LOGIN PROCESS COMPLETED SUCCESSFULLY ===");
                return apiResponse.Data;
            }
            else
            {
                _lastError = apiResponse?.Message ?? "An error occurred during login";
                _logger.LogWarning("Login failed - Success: {Success}, Message: {Message}", 
                    apiResponse?.Success, apiResponse?.Message);
                _logger.LogWarning("=== LOGIN PROCESS FAILED ===");
                return null;
            }
        }
        catch (HttpRequestException httpEx)
        {
            _lastError = "Network error occurred during login. Please check your connection.";
            _logger.LogError(httpEx, "=== HTTP REQUEST EXCEPTION === Network error during login for user: {Username}. Message: {Message}", loginDto.Username, httpEx.Message);
            _logger.LogError("HttpRequestException Data: {Data}", httpEx.Data);
            return null;
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            _lastError = "Login request timed out. Please try again.";
            _logger.LogError(tcEx, "=== TIMEOUT EXCEPTION === Request timeout during login for user: {Username}", loginDto.Username);
            return null;
        }
        catch (TaskCanceledException tcEx)
        {
            _lastError = "Login request was cancelled. Please try again.";
            _logger.LogError(tcEx, "=== CANCELLATION EXCEPTION === Request cancelled during login for user: {Username}", loginDto.Username);
            return null;
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred during login. Please try again.";
            _logger.LogError(ex, "=== GENERAL EXCEPTION IN LOGIN PROCESS === Exception during login for user: {Username}. Type: {ExceptionType}, Message: {Message}", 
                loginDto.Username, ex.GetType().Name, ex.Message);
            _logger.LogError("Exception StackTrace: {StackTrace}", ex.StackTrace);
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

            _logger.LogInformation("Getting current user from API");
            _logger.LogInformation("HttpClient BaseAddress for GetCurrentUser: {BaseAddress}", _httpClient.BaseAddress);
            
            // Set Authorization header for this request
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var requestUri = new Uri(_httpClient.BaseAddress!, "/api/auth/me");
            _logger.LogInformation("GetCurrentUser request URI: {RequestUri}", requestUri);
            
            var requestStartTime = DateTime.UtcNow;
            var response = await _httpClient.GetAsync("/api/auth/me");
            var requestDuration = DateTime.UtcNow - requestStartTime;
            
            _logger.LogInformation("GetCurrentUser request completed with status: {StatusCode} in {Duration}ms", response.StatusCode, requestDuration.TotalMilliseconds);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("GetCurrentUser API response: {Response}", jsonContent);
                
                // Parse the ApiResponse wrapper to extract the UserDto data
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("Successfully retrieved current user: {Username}", apiResponse.Data.Username);
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API response was not successful or contained no data");
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
            _logger.LogInformation("Getting token from client-side storage");
            var token = await _sessionTokenService.GetTokenAsync();
            _logger.LogInformation("Token from storage: {HasToken}", !string.IsNullOrEmpty(token));
            
            // Also log HttpClient base address here to debug service discovery
            _logger.LogInformation("HttpClient BaseAddress in GetTokenAsync: {BaseAddress}", _httpClient.BaseAddress);
            
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
            _logger.LogInformation("Setting token in client-side storage");
            await _sessionTokenService.SetTokenAsync(token);
            _logger.LogInformation("Token set in storage successfully");
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