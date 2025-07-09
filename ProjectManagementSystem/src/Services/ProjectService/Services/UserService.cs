using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Text.Json;

namespace ProjectManagementSystem.ProjectService.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserService(HttpClient httpClient, ILogger<UserService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        try
        {
            _logger.LogInformation("Fetching user data for user ID: {UserId}", userId);
            
            // Get authorization header from current request and forward it
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    System.Net.Http.Headers.AuthenticationHeaderValue.Parse(authHeader);
                _logger.LogDebug("Authorization header set for UserService call");
            }
            else
            {
                _logger.LogWarning("No authorization header found in current request for user {UserId}", userId);
            }
            
            var response = await _httpClient.GetAsync($"api/internaluser/{userId}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch user {UserId}. Status: {StatusCode}", userId, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, _jsonOptions);
            
            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                _logger.LogInformation("Successfully fetched user data for user ID: {UserId}", userId);
                return apiResponse.Data;
            }

            _logger.LogWarning("API response was not successful for user ID: {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user data for user ID: {UserId}", userId);
            return null;
        }
    }

    public async Task<IList<UserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds)
    {
        var users = new List<UserDto>();
        var userIdList = userIds.ToList();

        if (!userIdList.Any())
        {
            return users;
        }

        _logger.LogInformation("Fetching user data for {Count} users", userIdList.Count);

        // Fetch users in parallel to improve performance
        var tasks = userIdList.Select(async userId =>
        {
            var user = await GetUserByIdAsync(userId);
            return new { UserId = userId, User = user };
        });

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            if (result.User != null)
            {
                users.Add(result.User);
            }
            else
            {
                _logger.LogWarning("Could not fetch user data for user ID: {UserId}", result.UserId);
            }
        }

        _logger.LogInformation("Successfully fetched {FetchedCount} out of {RequestedCount} users", 
            users.Count, userIdList.Count);

        return users;
    }
}