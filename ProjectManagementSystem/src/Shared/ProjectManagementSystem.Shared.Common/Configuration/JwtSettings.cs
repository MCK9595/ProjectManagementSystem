namespace ProjectManagementSystem.Shared.Common.Configuration;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 7;
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey) || SecretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long");
        }
        
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("JWT Issuer must be configured");
        }
        
        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("JWT Audience must be configured");
        }
        
        if (AccessTokenExpiryMinutes <= 0)
        {
            throw new InvalidOperationException("AccessTokenExpiryMinutes must be greater than 0");
        }
        
        if (RefreshTokenExpiryDays <= 0)
        {
            throw new InvalidOperationException("RefreshTokenExpiryDays must be greater than 0");
        }
    }
}