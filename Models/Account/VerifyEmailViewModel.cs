using System.ComponentModel.DataAnnotations;

namespace VaultEncryptionPOC.Models.Account;

public class VerifyEmailViewModel
{
    [Required(ErrorMessage = "The verification token is missing. Open the complete verification link from your email.")]
    [StringLength(500)]
    [Display(Name = "Verification token")]
    public string Token { get; set; } = string.Empty;
}
