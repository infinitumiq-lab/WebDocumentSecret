namespace VaultEncryptionPOC.Services.Api;
public class ApiResultWithData<T>
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
}
