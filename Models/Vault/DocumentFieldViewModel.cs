namespace VaultEncryptionPOC.Models.Vault;
public class DocumentFieldViewModel
{
    public string KeyName { get; set; } = string.Empty;
    public string InputType { get; set; } = "text";
    public List<string>? Options { get; set; }
}
