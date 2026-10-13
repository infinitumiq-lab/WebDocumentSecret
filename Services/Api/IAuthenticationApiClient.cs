using VaultEncryptionPOC.Models.Account;

namespace VaultEncryptionPOC.Services.Api;

public interface IAuthenticationApiClient
{
    Task<ApiResult> RegisterAsync(RegisterViewModel model);
    Task<ApiResult> ValidateSignupTokenAsync(string token);
    Task<ApiResult> LoginAsync(LoginViewModel model);
    Task<ApiResult> ForgotPasswordAsync(ForgotPasswordViewModel model);
    Task<ApiResult> ResetPasswordAsync(ResetPasswordViewModel model);
}
