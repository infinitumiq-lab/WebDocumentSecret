using VaultEncryptionPOC.Models.Vault;

namespace VaultEncryptionPOC.Services.Api;

public interface IDocumentApiClient
{
    Task<ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>> GetFormSchemasAsync();
    Task<ApiResultWithData<string>> SaveEncryptedDocumentAsync(string encryptedEnvelope, string? documentTypeId);
    Task<ApiResultWithData<EncryptedDocumentRecord>> GetEncryptedDocumentAsync(string recordId);
}
