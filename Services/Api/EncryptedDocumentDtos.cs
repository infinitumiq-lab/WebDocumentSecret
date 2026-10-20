namespace VaultEncryptionPOC.Services.Api;

/// <summary>Request accepted by POST /api/EncryptedDocuments.</summary>
public sealed class CreateEncryptedDocumentRequest
{
    public string EncryptedEnvelope { get; set; } = string.Empty;
    public Guid? DocumentTypeId { get; set; }
}

/// <summary>Record returned by the encrypted-document API.</summary>
public sealed class EncryptedDocumentRecord
{
    public string RecordId { get; set; } = string.Empty;
    public string Envelope { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public Guid? DocumentTypeId { get; set; }
}
