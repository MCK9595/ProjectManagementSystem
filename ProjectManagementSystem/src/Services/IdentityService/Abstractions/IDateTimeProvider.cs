namespace ProjectManagementSystem.IdentityService.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}