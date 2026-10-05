using System.ComponentModel.DataAnnotations;


namespace VaultEncryptionPOC.Models
{
    public class FormModel
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string DocumentType { get; set; } = string.Empty;

        [Required]
        public string DocumentNumber { get; set; } = string.Empty;

        public string Issuer { get; set; } = string.Empty;
        
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; } 

    }
}
