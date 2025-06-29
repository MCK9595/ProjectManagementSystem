using Microsoft.Extensions.Logging;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net;

namespace ProjectManagementSystem.WebApp.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;
    private string? _lastError;

    public UserService(
        IHttpClientFactory httpClientFactory,
        ILogger<UserService> logger)
    {
        _logger = logger;
        _logger.LogInformation("=== USERSERVICE CONSTRUCTOR STARTED ===");
        
        try
        {
            _httpClient = httpClientFactory.CreateClient("ApiGateway");
            _logger.LogInformation("HttpClient created with name: ApiGateway");
            _logger.LogInformation("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress?.ToString() ?? "NULL");
            _logger.LogInformation("HttpClient Timeout: {Timeout}", _httpClient.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create HttpClient");
            throw;
        }
        
        _logger.LogInformation("=== USERSERVICE CONSTRUCTOR COMPLETED ===");
    }

    public async Task<PagedResult<UserListDto>?> GetUsersAsync(UserSearchRequest request)
    {
        try
        {
            _logger.LogInformation("=== GET USERS REQUEST STARTED ===");
            _logger.LogInformation("Search parameters - Term: '{SearchTerm}', Role: '{Role}', IsActive: {IsActive}, Page: {Page}, PageSize: {PageSize}", 
                request.SearchTerm, request.Role, request.IsActive, request.PageNumber, request.PageSize);
            _lastError = null;

            // Build query string
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                queryParams.Add($"SearchTerm={Uri.EscapeDataString(request.SearchTerm)}");
            if (!string.IsNullOrWhiteSpace(request.Role))
                queryParams.Add($"Role={Uri.EscapeDataString(request.Role)}");
            if (request.IsActive.HasValue)
                queryParams.Add($"IsActive={request.IsActive.Value}");
            queryParams.Add($"PageNumber={request.PageNumber}");
            queryParams.Add($"PageSize={request.PageSize}");
            if (!string.IsNullOrWhiteSpace(request.SortBy))
                queryParams.Add($"SortBy={Uri.EscapeDataString(request.SortBy)}");
            if (!string.IsNullOrWhiteSpace(request.SortDirection))
                queryParams.Add($"SortDirection={Uri.EscapeDataString(request.SortDirection)}");

            var queryString = string.Join("&", queryParams);
            var requestUri = $"/api/usermanagement?{queryString}";
            
            _logger.LogInformation("Full request URI: {RequestUri}", requestUri);
            
            var response = await _httpClient.GetAsync(requestUri);
            _logger.LogInformation("GetUsers request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Response content received - Length: {Length}", content.Length);
                
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<UserListDto>>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("Users retrieved successfully - Total: {TotalCount}, Items: {ItemCount}", 
                        apiResponse.Data.TotalCount, apiResponse.Data.Items.Count);
                    return apiResponse.Data;
                }
                else
                {
                    _lastError = apiResponse?.Message ?? "Failed to retrieve users";
                    _logger.LogWarning("API returned unsuccessful response: {Error}", _lastError);
                    return null;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("GetUsers failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while retrieving users";
            _logger.LogError(ex, "Exception in GetUsersAsync");
            return null;
        }
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        try
        {
            _logger.LogInformation("=== GET USER BY ID REQUEST STARTED === ID: {UserId}", userId);
            _lastError = null;

            var requestUri = $"/api/usermanagement/{userId}";
            _logger.LogInformation("Request URI: {RequestUri}", requestUri);
            
            var response = await _httpClient.GetAsync(requestUri);
            _logger.LogInformation("GetUserById request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("User retrieved successfully - ID: {UserId}, Username: '{Username}'", 
                        apiResponse.Data.Id, apiResponse.Data.Username);
                    return apiResponse.Data;
                }
                else
                {
                    _lastError = apiResponse?.Message ?? "Failed to retrieve user";
                    _logger.LogWarning("API returned unsuccessful response: {Error}", _lastError);
                    return null;
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _lastError = $"User with ID {userId} not found";
                _logger.LogWarning("User not found - ID: {UserId}", userId);
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("GetUserById failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while retrieving the user";
            _logger.LogError(ex, "Exception in GetUserByIdAsync for ID: {UserId}", userId);
            return null;
        }
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            _logger.LogInformation("=== CREATE USER REQUEST STARTED ===");
            _logger.LogInformation("Creating user - Username: '{Username}', Email: '{Email}', Role: '{Role}'", 
                request.Username, request.Email, request.Role);
            _lastError = null;

            var requestUri = "/api/usermanagement";
            _logger.LogInformation("Request URI: {RequestUri}", requestUri);
            
            var response = await _httpClient.PostAsJsonAsync(requestUri, request);
            _logger.LogInformation("CreateUser request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("User created successfully - ID: {UserId}, Username: '{Username}'", 
                        apiResponse.Data.Id, apiResponse.Data.Username);
                    return apiResponse.Data;
                }
                else
                {
                    _lastError = apiResponse?.Message ?? "Failed to create user";
                    _logger.LogWarning("API returned unsuccessful response: {Error}", _lastError);
                    return null;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("CreateUser failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while creating the user";
            _logger.LogError(ex, "Exception in CreateUserAsync for username: {Username}", request.Username);
            return null;
        }
    }

    public async Task<UserDto?> UpdateUserAsync(int userId, UpdateUserRequest request)
    {
        try
        {
            _logger.LogInformation("=== UPDATE USER REQUEST STARTED === ID: {UserId}", userId);
            _lastError = null;

            var requestUri = $"/api/usermanagement/{userId}";
            _logger.LogInformation("Request URI: {RequestUri}", requestUri);
            
            var response = await _httpClient.PutAsJsonAsync(requestUri, request);
            _logger.LogInformation("UpdateUser request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("User updated successfully - ID: {UserId}, Username: '{Username}'", 
                        apiResponse.Data.Id, apiResponse.Data.Username);
                    return apiResponse.Data;
                }
                else
                {
                    _lastError = apiResponse?.Message ?? "Failed to update user";
                    _logger.LogWarning("API returned unsuccessful response: {Error}", _lastError);
                    return null;
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _lastError = $"User with ID {userId} not found";
                _logger.LogWarning("User not found for update - ID: {UserId}", userId);
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("UpdateUser failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while updating the user";
            _logger.LogError(ex, "Exception in UpdateUserAsync for ID: {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        try
        {
            _logger.LogInformation("=== DELETE USER REQUEST STARTED === ID: {UserId}", userId);
            _lastError = null;

            var requestUri = $"/api/usermanagement/{userId}";
            _logger.LogInformation("Request URI: {RequestUri}", requestUri);
            
            var response = await _httpClient.DeleteAsync(requestUri);
            _logger.LogInformation("DeleteUser request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("User deleted successfully - ID: {UserId}", userId);
                return true;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _lastError = $"User with ID {userId} not found";
                _logger.LogWarning("User not found for deletion - ID: {UserId}", userId);
                return false;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("DeleteUser failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while deleting the user";
            _logger.LogError(ex, "Exception in DeleteUserAsync for ID: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ChangeUserRoleAsync(int userId, string role)
    {
        try
        {
            _logger.LogInformation("=== CHANGE USER ROLE REQUEST STARTED === ID: {UserId}, Role: '{Role}'", userId, role);
            _lastError = null;

            var requestUri = $"/api/usermanagement/{userId}/role";
            var request = new ChangeRoleRequest { Role = role };
            
            var response = await _httpClient.PutAsJsonAsync(requestUri, request);
            _logger.LogInformation("ChangeUserRole request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("User role changed successfully - ID: {UserId}, Role: '{Role}'", userId, role);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("ChangeUserRole failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while changing user role";
            _logger.LogError(ex, "Exception in ChangeUserRoleAsync for ID: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ChangeUserStatusAsync(int userId, bool isActive)
    {
        try
        {
            _logger.LogInformation("=== CHANGE USER STATUS REQUEST STARTED === ID: {UserId}, Active: {IsActive}", userId, isActive);
            _lastError = null;

            var requestUri = $"/api/usermanagement/{userId}/status";
            var request = new ChangeStatusRequest { IsActive = isActive };
            
            var response = await _httpClient.PutAsJsonAsync(requestUri, request);
            _logger.LogInformation("ChangeUserStatus request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("User status changed successfully - ID: {UserId}, Active: {IsActive}", userId, isActive);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("ChangeUserStatus failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while changing user status";
            _logger.LogError(ex, "Exception in ChangeUserStatusAsync for ID: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        try
        {
            _logger.LogInformation("=== CHANGE PASSWORD REQUEST STARTED === ID: {UserId}", userId);
            _lastError = null;

            var requestUri = $"/api/usermanagement/{userId}/change-password";
            
            var response = await _httpClient.PostAsJsonAsync(requestUri, request);
            _logger.LogInformation("ChangePassword request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Password changed successfully - ID: {UserId}", userId);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(errorContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _lastError = apiResponse?.Message ?? $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                }
                catch
                {
                    _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                }
                
                _logger.LogError("ChangePassword failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while changing the password";
            _logger.LogError(ex, "Exception in ChangePasswordAsync for ID: {UserId}", userId);
            return false;
        }
    }

    public async Task<List<string>?> GetAvailableRolesAsync()
    {
        try
        {
            _logger.LogInformation("=== GET AVAILABLE ROLES REQUEST STARTED ===");
            _lastError = null;

            var requestUri = "/api/usermanagement/roles";
            _logger.LogInformation("Request URI: {RequestUri}", requestUri);
            
            var response = await _httpClient.GetAsync(requestUri);
            _logger.LogInformation("GetAvailableRoles request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<string>>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("Available roles retrieved successfully - Count: {RoleCount}", apiResponse.Data.Count);
                    return apiResponse.Data;
                }
                else
                {
                    _lastError = apiResponse?.Message ?? "Failed to retrieve available roles";
                    _logger.LogWarning("API returned unsuccessful response: {Error}", _lastError);
                    return null;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("GetAvailableRoles failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while retrieving available roles";
            _logger.LogError(ex, "Exception in GetAvailableRolesAsync");
            return null;
        }
    }

    public async Task<object?> GetUserStatisticsAsync()
    {
        try
        {
            _logger.LogInformation("=== GET USER STATISTICS REQUEST STARTED ===");
            _lastError = null;

            var requestUri = "/api/usermanagement/statistics";
            _logger.LogInformation("Request URI: {RequestUri}", requestUri);
            
            var response = await _httpClient.GetAsync(requestUri);
            _logger.LogInformation("GetUserStatistics request completed with status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("User statistics retrieved successfully");
                    return apiResponse.Data;
                }
                else
                {
                    _lastError = apiResponse?.Message ?? "Failed to retrieve user statistics";
                    _logger.LogWarning("API returned unsuccessful response: {Error}", _lastError);
                    return null;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _lastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("GetUserStatistics failed - Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _lastError = "An error occurred while retrieving user statistics";
            _logger.LogError(ex, "Exception in GetUserStatisticsAsync");
            return null;
        }
    }

    public string? GetLastError()
    {
        return _lastError;
    }
}