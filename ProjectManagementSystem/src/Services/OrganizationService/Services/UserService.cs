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
}