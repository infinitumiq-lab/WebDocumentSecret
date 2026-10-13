using System.ComponentModel.DataAnnotations;

namespace VaultEncryptionPOC.Models.Account;

public class VerifyEmailViewModel
{
    [Required(ErrorMessage = "The verification token is missing. Open the complete link from your email or paste the token below.")]
    [StringLength(500)]
    [Display(Name = "Verification token")]
    public string Token { get; set; } = string.Empty;
}
