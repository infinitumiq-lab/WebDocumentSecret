using VaultEncryptionPOC.Models.Vault;
namespace VaultEncryptionPOC.Services.Api;
public interface IDocumentApiClient
{
    Task<ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>> GetFormSchemasAsync();
}
