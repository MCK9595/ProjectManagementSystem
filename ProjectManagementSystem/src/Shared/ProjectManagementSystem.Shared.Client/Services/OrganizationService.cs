using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Client.Models;
using ProjectManagementSystem.Shared.Models.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ProjectManagementSystem.Shared.Client.Services;

public interface IOrganizationService
{
    Task<PagedResult<OrganizationDto>?> GetOrganizationsAsync(int pageNumber = 1, int pageSize = 10);
    Task<OrganizationDto?> GetOrganizationAsync(Guid id);
    Task<OrganizationDto?> CreateOrganizationAsync(CreateOrganizationDto organizationDto);
    Task<OrganizationDto?> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto organizationDto);
    Task<bool> DeleteOrganizationAsync(Guid id);
    Task<PagedResult<OrganizationMemberDto>?> GetOrganizationMembersAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10);
    Task<OrganizationMemberDto?> AddMemberAsync(Guid organizationId, AddMemberDto addMemberDto);
    Task<OrganizationMemberDto?> AddMemberByEmailAsync(Guid organizationId, AddMemberByEmailDto addMemberDto);
    Task<UserDto?> CheckUserExistsByEmailAsync(string email);
    Task<OrganizationMemberDto?> FindAndAddMemberAsync(Guid organizationId, FindUserByEmailDto findUserDto);
    Task<OrganizationMemberDto?> CreateAndAddMemberAsync(Guid organizationId, CreateUserAndAddMemberDto createUserDto);
    Task<bool> RemoveMemberAsync(Guid organizationId, int userId);
    Task<bool> UpdateMemberRoleAsync(Guid organizationId, int userId, string role);
    Task<bool> TransferOwnershipAsync(Guid organizationId, int newOwnerId);
}

public class OrganizationService : IOrganizationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(IHttpClientFactory httpClientFactory, ILogger<OrganizationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ApiGateway");
        _logger = logger;
        
        _logger.LogDebug("OrganizationService initialized with HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
    }

    public async Task<PagedResult<OrganizationDto>?> GetOrganizationsAsync(int pageNumber = 1, int pageSize = 10)
    {
        var requestUrl = $"/api/organizations?pageNumber={pageNumber}&pageSize={pageSize}";
        _logger.LogDebug("GetOrganizationsAsync called - requesting: {RequestUrl}", requestUrl);
        
        try
        {
            _logger.LogDebug("Making HTTP GET request to: {RequestUrl} with BaseAddress: {BaseAddress}", requestUrl, _httpClient.BaseAddress);
            var response = await _httpClient.GetAsync(requestUrl);
            
            _logger.LogDebug("HTTP response received - StatusCode: {StatusCode}, IsSuccessStatusCode: {Success}", 
                response.StatusCode, response.IsSuccessStatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Response content length: {ContentLength}", jsonContent.Length);
                
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in AddMemberAsync for organization {OrganizationId}", organizationId);
        }

        return null;
    }

    public async Task<OrganizationMemberDto?> AddMemberByEmailAsync(Guid organizationId, AddMemberByEmailDto addMemberDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/organizations/{organizationId}/members/by-email", addMemberDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationMemberDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for add member by email to organization {OrganizationId}: {Message}", organizationId, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for add member by email to organization {OrganizationId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    organizationId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in AddMemberByEmailAsync for organization {OrganizationId}", organizationId);
        }

        return null;
    }

    public async Task<UserDto?> CheckUserExistsByEmailAsync(string email)
    {
        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var response = await _httpClient.GetAsync($"/api/internaluser/check-email/{encodedEmail}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("User not found for email: {Email}", email);
                return null;
            }
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for check user email {Email}: {Message}", email, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for check user email {Email} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    email, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CheckUserExistsByEmailAsync for email {Email}", email);
        }

        return null;
    }

    public async Task<OrganizationMemberDto?> FindAndAddMemberAsync(Guid organizationId, FindUserByEmailDto findUserDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/organizations/{organizationId}/members/find-and-add", findUserDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationMemberDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for find and add member to organization {OrganizationId}: {Message}", organizationId, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for find and add member to organization {OrganizationId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    organizationId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FindAndAddMemberAsync for organization {OrganizationId}", organizationId);
        }

        return null;
    }

    public async Task<OrganizationMemberDto?> CreateAndAddMemberAsync(Guid organizationId, CreateUserAndAddMemberDto createUserDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/organizations/{organizationId}/members/create-and-add", createUserDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationMemberDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for create and add member to organization {OrganizationId}: {Message}", organizationId, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for create and add member to organization {OrganizationId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    organizationId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CreateAndAddMemberAsync for organization {OrganizationId}", organizationId);
        }

        return null;
    }

    public async Task<bool> RemoveMemberAsync(Guid organizationId, int userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/organizations/{organizationId}/members/{userId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in RemoveMemberAsync for organization {OrganizationId}, user {UserId}", organizationId, userId);
        }

        return false;
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid organizationId, int userId, string role)
    {
        try
        {
            var request = new { Role = role };
            var response = await _httpClient.PutAsJsonAsync($"/api/organizations/{organizationId}/members/{userId}/role", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateMemberRoleAsync for organization {OrganizationId}, user {UserId}", organizationId, userId);
        }

        return false;
    }

    public async Task<bool> TransferOwnershipAsync(Guid organizationId, int newOwnerId)
    {
        try
        {
            var request = new { NewOwnerId = newOwnerId };
            var response = await _httpClient.PostAsJsonAsync($"/api/organizations/{organizationId}/transfer-ownership", request);
            
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
                _logger.LogWarning("HTTP request failed for transfer ownership - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in TransferOwnershipAsync for organization {OrganizationId}", organizationId);
        }

        return false;
    }
}