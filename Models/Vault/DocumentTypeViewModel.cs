namespace VaultEncryptionPOC.Models.Vault;
public class DocumentTypeViewModel
{
    public string DocumentTypeId { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public List<DocumentFieldViewModel> Fields { get; set; } = new();
}
