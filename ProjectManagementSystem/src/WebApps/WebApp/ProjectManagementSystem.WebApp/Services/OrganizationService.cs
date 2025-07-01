using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectManagementSystem.WebApp.Services;

public class OrganizationService : IOrganizationService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<OrganizationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ApiGateway");
        _authService = authService;
        _logger = logger;
        
        _logger.LogDebug("OrganizationService initialized with HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
    }

    public async Task<PagedResult<OrganizationDto>?> GetOrganizationsAsync(int pageNumber = 1, int pageSize = 10)
    {
        var requestUrl = $"/api/organizations?pageNumber={pageNumber}&pageSize={pageSize}";
        _logger.LogDebug("GetOrganizationsAsync called - requesting: {RequestUrl}", requestUrl);
        
        try
        {
            _logger.LogDebug("Setting auth header for request");
            await SetAuthHeaderAsync();
            
            _logger.LogDebug("Making HTTP GET request to: {RequestUrl} with BaseAddress: {BaseAddress}", requestUrl, _httpClient.BaseAddress);
            var response = await _httpClient.GetAsync(requestUrl);
            
            _logger.LogDebug("HTTP response received - StatusCode: {StatusCode}, IsSuccessStatusCode: {Success}", 
                response.StatusCode, response.IsSuccessStatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Response content length: {ContentLength}", jsonContent.Length);
                _logger.LogDebug("Response content: {JsonContent}", jsonContent);
                
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<OrganizationDto>>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogDebug("Deserialization successful - TotalCount: {TotalCount}", apiResponse.Data.TotalCount);
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response: {Message}", apiResponse?.Message);
                    return null;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetOrganizationsAsync - Type: {ExceptionType}, Message: {ExceptionMessage}", 
                ex.GetType().Name, ex.Message);
        }

        return null;
    }

    public async Task<OrganizationDto?> GetOrganizationAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/organizations/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for organization {OrganizationId}: {Message}", id, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for organization {OrganizationId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetOrganizationAsync for organization {OrganizationId}", id);
        }

        return null;
    }

    public async Task<OrganizationDto?> CreateOrganizationAsync(CreateOrganizationDto organizationDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("/api/organizations", organizationDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for create organization: {Message}", apiResponse?.Message);
                    throw new InvalidOperationException(apiResponse?.Message ?? "Unknown error occurred");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Organization creation failed due to conflict - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationDto>>(errorContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    throw new InvalidOperationException(errorResponse?.Message ?? "Organization with this name already exists");
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException("Organization with this name already exists");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Validation failed for create organization - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationDto>>(errorContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    var validationErrors = errorResponse?.Errors?.Any() == true 
                        ? string.Join(", ", errorResponse.Errors)
                        : errorResponse?.Message ?? "Validation failed";
                    throw new ArgumentException(validationErrors);
                }
                catch (JsonException)
                {
                    throw new ArgumentException("Validation failed");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for create organization - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is HttpRequestException)
        {
            throw; // Re-throw specific exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CreateOrganizationAsync");
            throw new Exception("An unexpected error occurred while creating the organization");
        }
    }

    public async Task<OrganizationDto?> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto organizationDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"/api/organizations/{id}", organizationDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for update organization {OrganizationId}: {Message}", id, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for update organization {OrganizationId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateOrganizationAsync for organization {OrganizationId}", id);
        }

        return null;
    }

    public async Task<bool> DeleteOrganizationAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/organizations/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return apiResponse?.Success == true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for delete organization {OrganizationId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeleteOrganizationAsync for organization {OrganizationId}", id);
        }

        return false;
    }

    public async Task<PagedResult<OrganizationMemberDto>?> GetOrganizationMembersAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/organizations/{organizationId}/members?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<OrganizationMemberDto>>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for get organization members {OrganizationId}: {Message}", organizationId, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for get organization members {OrganizationId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    organizationId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetOrganizationMembersAsync for organization {OrganizationId}", organizationId);
        }

        return null;
    }

    public async Task<OrganizationMemberDto?> AddMemberAsync(Guid organizationId, AddMemberDto addMemberDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync($"/api/organizations/{organizationId}/members", addMemberDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationMemberDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return apiResponse?.Data;
            }
        }
        catch (Exception)
        {
            // Log exception
        }

        return null;
    }

    public async Task<bool> RemoveMemberAsync(Guid organizationId, int userId)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/organizations/{organizationId}/members/{userId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid organizationId, int userId, string role)
    {
        try
        {
            await SetAuthHeaderAsync();
            var request = new { Role = role };
            var response = await _httpClient.PutAsJsonAsync($"/api/organizations/{organizationId}/members/{userId}/role", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    private async Task SetAuthHeaderAsync()
    {
        _logger.LogDebug("SetAuthHeaderAsync called - getting token from auth service");
        var token = await _authService.GetTokenAsync();
        
        if (!string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("Token retrieved successfully - length: {TokenLength}", token.Length);
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _logger.LogDebug("Authorization header set with Bearer token");
        }
        else
        {
            _logger.LogWarning("No token available from auth service");
        }
    }
}