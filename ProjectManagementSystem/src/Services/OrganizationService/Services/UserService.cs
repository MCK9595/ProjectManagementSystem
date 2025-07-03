using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Text.Json;

namespace ProjectManagementSystem.OrganizationService.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserService(HttpClient httpClient, ILogger<UserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        try
        {
            _logger.LogInformation("=== ORGANIZATION SERVICE - Fetching user data for user ID: {UserId} ===", userId);
            _logger.LogInformation("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            
            var requestUrl = $"api/internaluser/{userId}";
            _logger.LogInformation("Making request to: {RequestUrl} (BaseAddress: {BaseAddress})", requestUrl, _httpClient.BaseAddress);
            
            var response = await _httpClient.GetAsync(requestUrl);
            
            _logger.LogInformation("Response Status: {StatusCode} for user ID: {UserId}", response.StatusCode, userId);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to fetch user {UserId}. Status: {StatusCode}, Response: {ResponseContent}", 
                    userId, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Response Content for user {UserId}: {Content}", userId, content);
            
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, _jsonOptions);
            _logger.LogInformation("Deserialized API Response - Success: {Success}, Data null: {DataIsNull}", 
                apiResponse?.Success, apiResponse?.Data == null);
            
            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                _logger.LogInformation("Successfully fetched user data for user ID: {UserId}, Username: {Username}", 
                    userId, apiResponse.Data.Username);
                return apiResponse.Data;
            }

            _logger.LogWarning("API response was not successful for user ID: {UserId}. Success: {Success}, Message: {Message}", 
                userId, apiResponse?.Success, apiResponse?.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user data for user ID: {UserId}. Exception: {ExceptionMessage}", 
                userId, ex.Message);
            return null;
        }
    }

    public async Task<IList<UserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds)
    {
        var users = new List<UserDto>();
        var userIdList = userIds.ToList();

        _logger.LogInformation("=== ORGANIZATION SERVICE - GetUsersByIdsAsync called with {Count} user IDs: [{UserIds}] ===", 
            userIdList.Count, string.Join(", ", userIdList));

        if (!userIdList.Any())
        {
            _logger.LogInformation("No user IDs provided, returning empty list");
            return users;
        }

        // Fetch users in parallel to improve performance
        var tasks = userIdList.Select(async userId =>
        {
            _logger.LogInformation("Starting fetch for user ID: {UserId}", userId);
            var user = await GetUserByIdAsync(userId);
            _logger.LogInformation("Completed fetch for user ID: {UserId}, Success: {Success}", userId, user != null);
            return new { UserId = userId, User = user };
        });

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            if (result.User != null)
            {
                users.Add(result.User);
                _logger.LogInformation("Added user {UserId} ({Username}) to results", result.UserId, result.User.Username);
            }
            else
            {
                _logger.LogWarning("Could not fetch user data for user ID: {UserId}", result.UserId);
            }
        }

        _logger.LogInformation("=== ORGANIZATION SERVICE - GetUsersByIdsAsync completed: {FetchedCount} out of {RequestedCount} users ===", 
            users.Count, userIdList.Count);

        return users;
    }

    public async Task<UserDto?> FindOrCreateUserAsync(string email, string firstName, string lastName)
    {
        try
        {
            _logger.LogInformation("=== ORGANIZATION SERVICE - FindOrCreateUserAsync called for email: {Email} ===", email);
            
            var requestData = new FindOrCreateUserRequest
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName
            };

            var requestUrl = "api/internaluser/find-or-create";
            _logger.LogInformation("Making request to: {RequestUrl} (BaseAddress: {BaseAddress})", requestUrl, _httpClient.BaseAddress);
            
            var response = await _httpClient.PostAsJsonAsync(requestUrl, requestData, _jsonOptions);
            
            _logger.LogInformation("Response Status: {StatusCode} for email: {Email}", response.StatusCode, email);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to find or create user {Email}. Status: {StatusCode}, Response: {ResponseContent}", 
                    email, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Response Content for email {Email}: {Content}", email, content);
            
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, _jsonOptions);
            _logger.LogInformation("Deserialized API Response - Success: {Success}, Data null: {DataIsNull}", 
                apiResponse?.Success, apiResponse?.Data == null);
            
            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                _logger.LogInformation("Successfully found/created user for email: {Email}, User ID: {UserId}, Username: {Username}", 
                    email, apiResponse.Data.Id, apiResponse.Data.Username);
                return apiResponse.Data;
            }

            _logger.LogWarning("API response was not successful for email: {Email}. Success: {Success}, Message: {Message}", 
                email, apiResponse?.Success, apiResponse?.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding or creating user for email: {Email}. Exception: {ExceptionMessage}", 
                email, ex.Message);
            return null;
        }
    }

    public async Task<UserDto?> CheckUserExistsByEmailAsync(string email)
    {
        try
        {
            _logger.LogInformation("=== ORGANIZATION SERVICE - CheckUserExistsByEmailAsync called for email: {Email} ===", email);
            
            var requestUrl = $"api/internaluser/check-email/{Uri.EscapeDataString(email)}";
            _logger.LogInformation("Making request to: {RequestUrl} (BaseAddress: {BaseAddress})", requestUrl, _httpClient.BaseAddress);
            
            var response = await _httpClient.GetAsync(requestUrl);
            
            _logger.LogInformation("Response Status: {StatusCode} for email: {Email}", response.StatusCode, email);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("User not found for email: {Email}", email);
                return null;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to check user email {Email}. Status: {StatusCode}, Response: {ResponseContent}", 
                    email, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Response Content for email {Email}: {Content}", email, content);
            
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, _jsonOptions);
            _logger.LogInformation("Deserialized API Response - Success: {Success}, Data null: {DataIsNull}", 
                apiResponse?.Success, apiResponse?.Data == null);
            
            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                _logger.LogInformation("Successfully found user for email: {Email}, User ID: {UserId}, Username: {Username}", 
                    email, apiResponse.Data.Id, apiResponse.Data.Username);
                return apiResponse.Data;
            }

            _logger.LogWarning("API response was not successful for email: {Email}. Success: {Success}, Message: {Message}", 
                email, apiResponse?.Success, apiResponse?.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user email: {Email}. Exception: {ExceptionMessage}", 
                email, ex.Message);
            return null;
        }
    }

    public async Task<UserDto?> CreateUserWithPasswordAsync(CreateUserWithPasswordRequest request)
    {
        try
        {
            _logger.LogInformation("=== ORGANIZATION SERVICE - CreateUserWithPasswordAsync called for email: {Email} ===", request.Email);
            
            var requestUrl = "api/internaluser/create-with-password";
            _logger.LogInformation("Making request to: {RequestUrl} (BaseAddress: {BaseAddress})", requestUrl, _httpClient.BaseAddress);
            
            var response = await _httpClient.PostAsJsonAsync(requestUrl, request, _jsonOptions);
            
            _logger.LogInformation("Response Status: {StatusCode} for email: {Email}", response.StatusCode, request.Email);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to create user with password {Email}. Status: {StatusCode}, Response: {ResponseContent}", 
                    request.Email, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Response Content for email {Email}: {Content}", request.Email, content);
            
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, _jsonOptions);
            _logger.LogInformation("Deserialized API Response - Success: {Success}, Data null: {DataIsNull}", 
                apiResponse?.Success, apiResponse?.Data == null);
            
            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                _logger.LogInformation("Successfully created user with password for email: {Email}, User ID: {UserId}, Username: {Username}", 
                    request.Email, apiResponse.Data.Id, apiResponse.Data.Username);
                return apiResponse.Data;
            }

            _logger.LogWarning("API response was not successful for email: {Email}. Success: {Success}, Message: {Message}", 
                request.Email, apiResponse?.Success, apiResponse?.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user with password for email: {Email}. Exception: {ExceptionMessage}", 
                request.Email, ex.Message);
            return null;
        }
    }
}