using System.ComponentModel.DataAnnotations;

namespace VaultEncryptionPOC.Models.Account;

public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the reset code from your email.")]
    [StringLength(100)]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your new password.")]
    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
