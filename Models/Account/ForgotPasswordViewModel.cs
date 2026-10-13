using System.ComponentModel.DataAnnotations;

namespace VaultEncryptionPOC.Models.Account;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
}
