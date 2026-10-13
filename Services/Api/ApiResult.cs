namespace VaultEncryptionPOC.Services.Api;

public class ApiResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Data { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
}
