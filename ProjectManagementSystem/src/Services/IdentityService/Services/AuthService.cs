using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.IdentityService.Abstractions;

namespace ProjectManagementSystem.IdentityService.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuthService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ILogger<AuthService> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
    {
        // Temporarily disable soft delete check until migration is properly applied
        var user = await _context.Users
            .Where(u => u.UserName == loginDto.Username || u.Email == loginDto.Username)
            .FirstOrDefaultAsync();
        
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Login attempt for non-existent, inactive, or deleted user: {Username}", loginDto.Username);
            return null;
        }

        // Check if user is locked out
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login attempt for locked out user: {Username}", loginDto.Username);
            return null;
        }

        // Check password
        var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, true);
        
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {Username} locked out due to too many failed login attempts", loginDto.Username);
            }
            else
            {
                _logger.LogWarning("Invalid password for user: {Username}", loginDto.Username);
            }
            return null;
        }

        // Successful login - update last login
        user.LastLoginAt = _dateTimeProvider.UtcNow;
        await _userManager.UpdateAsync(user);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id);

        _logger.LogInformation("User {Username} logged in successfully", loginDto.Username);

        return new AuthResponseDto
        {
            Token = accessToken,
            User = await MapUserToDtoAsync(user),
            ExpiresAt = _dateTimeProvider.UtcNow.AddMinutes(15) // Should match token expiry
        };
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var storedToken = await _tokenService.GetRefreshTokenAsync(refreshToken);
        
        if (storedToken == null || !storedToken.IsActive)
        {
            _logger.LogWarning("Invalid or expired refresh token provided");
            return null;
        }

        // Temporarily disable soft delete check until migration is properly applied
        var user = await _context.Users
            .Where(u => u.Id == storedToken.UserId)
            .FirstOrDefaultAsync();
            
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Refresh token belongs to inactive or deleted user");
            return null;
        }

        // Generate new tokens
        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id);

        // Revoke the old refresh token
        await _tokenService.RevokeRefreshTokenAsync(refreshToken, newRefreshToken.Token);

        _logger.LogInformation("Tokens refreshed for user {UserId}", user.Id);

        return new AuthResponseDto
        {
            Token = newAccessToken,
            User = await MapUserToDtoAsync(user),
            ExpiresAt = _dateTimeProvider.UtcNow.AddMinutes(15)
        };
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var storedToken = await _tokenService.GetRefreshTokenAsync(refreshToken);
        if (storedToken != null)
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
            _logger.LogInformation("User {UserId} logged out", storedToken.UserId);
            return true;
        }

        return false;
    }

    public async Task<UserDto?> GetCurrentUserAsync(int userId)
    {
        var user = await GetUserByIdAsync(userId);
        return user != null ? await MapUserToDtoAsync(user) : null;
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(int userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<ApplicationUser?> GetUserByUsernameAsync(string username)
    {
        return await _userManager.FindByNameAsync(username);
    }

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    private async Task<UserDto> MapUserToDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        
        return new UserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = roles.FirstOrDefault() ?? "User",
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsActive = user.IsActive
        };
    }
}