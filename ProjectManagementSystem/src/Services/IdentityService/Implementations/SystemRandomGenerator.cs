using System.Security.Cryptography;
using ProjectManagementSystem.IdentityService.Abstractions;

namespace ProjectManagementSystem.IdentityService.Implementations;

public class SystemRandomGenerator : IRandomGenerator
{
    public byte[] GenerateRandomBytes(int length)
    {
        var randomBytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return randomBytes;
    }

    public string GenerateBase64String(int byteLength)
    {
        var randomBytes = GenerateRandomBytes(byteLength);
        return Convert.ToBase64String(randomBytes);
    }
}