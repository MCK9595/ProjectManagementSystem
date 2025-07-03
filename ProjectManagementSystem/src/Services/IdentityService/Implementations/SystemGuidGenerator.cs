using ProjectManagementSystem.IdentityService.Abstractions;

namespace ProjectManagementSystem.IdentityService.Implementations;

public class SystemGuidGenerator : IGuidGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
    
    public string NewGuidString() => Guid.NewGuid().ToString();
}