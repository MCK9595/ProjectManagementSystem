using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.IdentityService.Abstractions;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.IdentityService.Tests.Services;

public class UserManagementServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<RoleManager<ApplicationRole>> _roleManagerMock;
    private readonly Mock<ILogger<UserManagementService>> _loggerMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly UserManagementService _userManagementService;
    private readonly DateTime _fixedDateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

    public UserManagementServiceTests()
    {
        // Setup In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        // Setup RoleManager mock
        var roleStoreMock = new Mock<IRoleStore<ApplicationRole>>();
        _roleManagerMock = new Mock<RoleManager<ApplicationRole>>(
            roleStoreMock.Object, null, null, null, null);

        // Setup other mocks
        _loggerMock = new Mock<ILogger<UserManagementService>>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        
        // Setup fixed time for consistent testing
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_fixedDateTime);

        _userManagementService = new UserManagementService(
            _context,
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _loggerMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task GetUsersAsync_WithSearchFilters_ReturnsFilteredResults()
    {
        // Arrange
        var users = new[]
        {
            new ApplicationUser { Id = 1, UserName = "john.doe", Email = "john@example.com", FirstName = "John", LastName = "Doe", Role = "User", IsActive = true, CreatedAt = _fixedDateTime.AddDays(-10), UpdatedAt = _fixedDateTime.AddDays(-10) },
            new ApplicationUser { Id = 2, UserName = "jane.smith", Email = "jane@example.com", FirstName = "Jane", LastName = "Smith", Role = "Admin", IsActive = true, CreatedAt = _fixedDateTime.AddDays(-5), UpdatedAt = _fixedDateTime.AddDays(-5) },
            new ApplicationUser { Id = 3, UserName = "bob.wilson", Email = "bob@example.com", FirstName = "Bob", LastName = "Wilson", Role = "User", IsActive = false, CreatedAt = _fixedDateTime.AddDays(-3), UpdatedAt = _fixedDateTime.AddDays(-3) }
        };

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        var request = new UserSearchRequest
        {
            SearchTerm = "john",
            Role = "User",
            IsActive = true,
            PageNumber = 1,
            PageSize = 10,
            SortBy = "username",
            SortDirection = "asc"
        };

        // Act
        var result = await _userManagementService.GetUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Username.Should().Be("john.doe");
        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetUsersAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var users = Enumerable.Range(1, 15)
            .Select(i => new ApplicationUser
            {
                Id = i,
                UserName = $"user{i:D2}",
                Email = $"user{i}@example.com",
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                Role = "User",
                IsActive = true,
                CreatedAt = _fixedDateTime.AddDays(-i),
                UpdatedAt = _fixedDateTime.AddDays(-i)
            })
            .ToArray();

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        var request = new UserSearchRequest
        {
            PageNumber = 2,
            PageSize = 5,
            SortBy = "createdAt",
            SortDirection = "desc"
        };

        // Act
        var result = await _userManagementService.GetUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(15);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.Items.First().Username.Should().Be("user06"); // Should be 6th item when sorted by CreatedAt desc
    }

    [Fact]
    public async Task GetUserByIdAsync_ExistingUser_ReturnsUserDto()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-10),
            UpdatedAt = _fixedDateTime.AddDays(-10),
            LastLoginAt = _fixedDateTime.AddHours(-1)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userManagementService.GetUserByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Username.Should().Be("testuser");
        result.Email.Should().Be("test@example.com");
        result.FirstName.Should().Be("Test");
        result.LastName.Should().Be("User");
        result.Role.Should().Be("User");
        result.IsActive.Should().BeTrue();
        result.CreatedAt.Should().Be(user.CreatedAt);
        result.LastLoginAt.Should().Be(user.LastLoginAt);
    }

    [Fact]
    public async Task GetUserByIdAsync_NonExistentUser_ReturnsNull()
    {
        // Act
        var result = await _userManagementService.GetUserByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateUserAsync_ValidRequest_CreatesAndReturnsUser()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            FirstName = "New",
            LastName = "User",
            Password = "NewPassword123!",
            Role = "User",
            IsActive = true
        };

        _roleManagerMock.Setup(x => x.RoleExistsAsync("User")).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((user, password) =>
            {
                user.Id = 1; // Simulate ID assignment
                _context.Users.Add(user);
                _context.SaveChanges();
            });
        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), request.Role))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userManagementService.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be(request.Username);
        result.Email.Should().Be(request.Email);
        result.FirstName.Should().Be(request.FirstName);
        result.LastName.Should().Be(request.LastName);
        result.Role.Should().Be(request.Role);
        result.IsActive.Should().Be(request.IsActive);

        // Verify methods were called
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password), Times.Once);
        _userManagerMock.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), request.Role), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ReturnsNull()
    {
        // Arrange
        var existingUser = new ApplicationUser
        {
            Id = 1,
            UserName = "existinguser",
            Email = "existing@example.com",
            FirstName = "Existing",
            LastName = "User",
            Role = "User",
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-1),
            UpdatedAt = _fixedDateTime.AddDays(-1)
        };

        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        var request = new CreateUserRequest
        {
            Username = "existinguser", // Duplicate username
            Email = "newemail@example.com",
            FirstName = "New",
            LastName = "User",
            Password = "NewPassword123!",
            Role = "User",
            IsActive = true
        };

        // Act
        var result = await _userManagementService.CreateUserAsync(request);

        // Assert
        result.Should().BeNull();
        
        // Verify no creation attempt was made
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_InvalidRole_ReturnsNull()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            FirstName = "New",
            LastName = "User",
            Password = "NewPassword123!",
            Role = "InvalidRole",
            IsActive = true
        };

        _roleManagerMock.Setup(x => x.RoleExistsAsync("InvalidRole")).ReturnsAsync(false);

        // Act
        var result = await _userManagementService.CreateUserAsync(request);

        // Assert
        result.Should().BeNull();
        
        // Verify no creation attempt was made
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_PasswordValidationFails_ReturnsNull()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            FirstName = "New",
            LastName = "User",
            Password = "weak", // Weak password
            Role = "User",
            IsActive = true
        };

        _roleManagerMock.Setup(x => x.RoleExistsAsync("User")).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        // Act
        var result = await _userManagementService.CreateUserAsync(request);

        // Assert
        result.Should().BeNull();
        
        // Verify role assignment was not attempted
        _userManagerMock.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_ValidRequest_UpdatesAndReturnsUser()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "old@example.com",
            FirstName = "Old",
            LastName = "Name",
            Role = "User",
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-10),
            UpdatedAt = _fixedDateTime.AddDays(-10)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new UpdateUserRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Role = "Admin",
            IsActive = false
        };

        _userManagerMock.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email)).ReturnsAsync((ApplicationUser?)null);
        _roleManagerMock.Setup(x => x.RoleExistsAsync(request.Role)).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });
        _userManagerMock.Setup(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(user, request.Role)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userManagementService.UpdateUserAsync(1, request);

        // Assert
        result.Should().NotBeNull();
        user.FirstName.Should().Be("Updated");
        user.LastName.Should().Be("Name");
        user.Email.Should().Be("updated@example.com");
        user.Role.Should().Be("Admin");
        user.IsActive.Should().BeFalse();
        user.UpdatedAt.Should().Be(_fixedDateTime);

        // Verify method calls
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
        _userManagerMock.Verify(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()), Times.Once);
        _userManagerMock.Verify(x => x.AddToRoleAsync(user, request.Role), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var request = new UpdateUserRequest
        {
            FirstName = "Updated",
            LastName = "Name"
        };

        _userManagerMock.Setup(x => x.FindByIdAsync("999")).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _userManagementService.UpdateUserAsync(999, request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUserAsync_ExistingUser_SoftDeletesUser()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-1),
            UpdatedAt = _fixedDateTime.AddDays(-1),
            DeletedAt = null
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userManagementService.DeleteUserAsync(1);

        // Assert
        result.Should().BeTrue();
        user.DeletedAt.Should().Be(_fixedDateTime);
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUserAsync_NonExistentUser_ReturnsFalse()
    {
        // Act
        var result = await _userManagementService.DeleteUserAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeUserRoleAsync_ValidRequest_ChangesRole()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            CreatedAt = _fixedDateTime.AddDays(-1),
            UpdatedAt = _fixedDateTime.AddDays(-1)
        };

        _userManagerMock.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _roleManagerMock.Setup(x => x.RoleExistsAsync("Admin")).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });
        _userManagerMock.Setup(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userManagementService.ChangeUserRoleAsync(1, "Admin");

        // Assert
        result.Should().BeTrue();
        user.Role.Should().Be("Admin");
        user.UpdatedAt.Should().Be(_fixedDateTime);

        // Verify method calls
        _userManagerMock.Verify(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()), Times.Once);
        _userManagerMock.Verify(x => x.AddToRoleAsync(user, "Admin"), Times.Once);
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ChangeUserStatusAsync_ValidRequest_ChangesStatus()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-1),
            UpdatedAt = _fixedDateTime.AddDays(-1)
        };

        _userManagerMock.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userManagementService.ChangeUserStatusAsync(1, false);

        // Assert
        result.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.UpdatedAt.Should().Be(_fixedDateTime);

        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UserExistsAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = _fixedDateTime.AddDays(-1),
            UpdatedAt = _fixedDateTime.AddDays(-1)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userManagementService.UserExistsAsync("testuser", "test@example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserExistsAsync_NonExistentUser_ReturnsFalse()
    {
        // Act
        var result = await _userManagementService.UserExistsAsync("nonexistent", "nonexistent@example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetTotalUsersCountAsync_WithUsers_ReturnsCorrectCount()
    {
        // Arrange
        var users = new[]
        {
            new ApplicationUser { Id = 1, UserName = "user1", Email = "user1@example.com", FirstName = "User1", LastName = "Test", CreatedAt = _fixedDateTime, UpdatedAt = _fixedDateTime },
            new ApplicationUser { Id = 2, UserName = "user2", Email = "user2@example.com", FirstName = "User2", LastName = "Test", CreatedAt = _fixedDateTime, UpdatedAt = _fixedDateTime },
            new ApplicationUser { Id = 3, UserName = "user3", Email = "user3@example.com", FirstName = "User3", LastName = "Test", CreatedAt = _fixedDateTime, UpdatedAt = _fixedDateTime }
        };

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userManagementService.GetTotalUsersCountAsync();

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task CanDeleteUserAsync_SelfDeletion_ReturnsFalse()
    {
        // Act
        var result = await _userManagementService.CanDeleteUserAsync(1, 1); // Same user ID

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanDeleteUserAsync_LastSystemAdmin_ReturnsFalse()
    {
        // Arrange
        var systemAdmin = new ApplicationUser
        {
            Id = 1,
            UserName = "admin",
            FirstName = "System",
            LastName = "Admin",
            Role = Roles.SystemAdmin,
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-1),
            UpdatedAt = _fixedDateTime.AddDays(-1)
        };

        _context.Users.Add(systemAdmin);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(systemAdmin);

        // Act
        var result = await _userManagementService.CanDeleteUserAsync(1, 2);

        // Assert
        result.Should().BeFalse(); // Only one SystemAdmin, cannot delete
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidRequest_ChangesPassword()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = _fixedDateTime.AddDays(-1),
            UpdatedAt = _fixedDateTime.AddDays(-1)
        };

        var request = new ChangePasswordRequest
        {
            NewPassword = "NewPassword123!"
        };

        _userManagerMock.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userManagementService.ChangePasswordAsync(1, request);

        // Assert
        result.Should().BeTrue();
        user.UpdatedAt.Should().Be(_fixedDateTime);
        
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}