using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.IdentityService.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
    {
        var user = await _userManager.FindByNameAsync(loginDto.Username) 
                  ?? await _userManager.FindByEmailAsync(loginDto.Username);
        
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Login attempt for non-existent or inactive user: {Username}", loginDto.Username);
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
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id);

        _logger.LogInformation("User {Username} logged in successfully", loginDto.Username);

        return new AuthResponseDto
        {
            Token = accessToken,
            User = await MapUserToDtoAsync(user),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15) // Should match token expiry
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

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Refresh token belongs to inactive user");
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
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
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