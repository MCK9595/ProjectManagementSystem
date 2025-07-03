namespace ProjectManagementSystem.IdentityService.Abstractions;

public interface IGuidGenerator
{
    Guid NewGuid();
    string NewGuidString();
}