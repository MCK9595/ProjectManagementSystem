using ProjectManagementSystem.IdentityService.Abstractions;

namespace ProjectManagementSystem.IdentityService.Implementations;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}