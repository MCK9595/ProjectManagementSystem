namespace ProjectManagementSystem.IdentityService.Abstractions;

public interface IRandomGenerator
{
    byte[] GenerateRandomBytes(int length);
    string GenerateBase64String(int byteLength);
}