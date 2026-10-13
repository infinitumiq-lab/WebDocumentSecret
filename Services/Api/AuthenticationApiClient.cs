using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VaultEncryptionPOC.Models.Account;

namespace VaultEncryptionPOC.Services.Api;

public class AuthenticationApiClient : IAuthenticationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthenticationApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AuthenticationApiClient(HttpClient httpClient, ILogger<AuthenticationApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiResult> RegisterAsync(RegisterViewModel model) => SendAsync(
        HttpMethod.Post, "/api/Users/signup", new
        {
            email = model.Email.Trim(), password = model.Password,
            firstName = model.FirstName.Trim(), lastName = model.LastName.Trim(),
            phoneNumber = model.PhoneNumber.Trim()
        }, "register");

    public async Task<ApiResult> LoginAsync(LoginViewModel model)
    {
        var result = await SendAsync(HttpMethod.Post, "/api/Auth/login", new
        {
            username = model.Username.Trim(), password = model.Password
        }, "login");

        if (!result.Success || string.IsNullOrWhiteSpace(result.Data)) return result;

        try
        {
            using var document = JsonDocument.Parse(result.Data);
            var root = document.RootElement;
            var data = TryGet(root, "data");
            var accessToken = GetString(data, "token") ?? GetString(data, "accessToken");
            var refreshToken = GetString(data, "refreshToken");
            var email = GetString(data, "email") ?? model.Username.Trim();
            var role = GetString(data, "role");

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("Login endpoint returned success without an access token.");
                return new ApiResult
                {
                    Success = false,
                    StatusCode = result.StatusCode,
                    Message = "We couldn't complete sign-in because the service did not return a valid session. Please try again."
                };
            }

            return new ApiResult
            {
                Success = true, StatusCode = result.StatusCode,
                Message = "Login successful.", Data = result.Data,
                AccessToken = accessToken, RefreshToken = refreshToken,
                Email = email, Role = role
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Login response could not be parsed.");
            return new ApiResult { Success = false, StatusCode = result.StatusCode,
                Message = "We couldn't complete sign-in because the service returned an unexpected response." };
        }
    }

    public Task<ApiResult> ValidateSignupTokenAsync(string token) => SendAsync(
        HttpMethod.Post, "/api/Auth/validate-token", new { token = token.Trim(), purpose = "Signup" }, "verify email");

    public Task<ApiResult> ForgotPasswordAsync(ForgotPasswordViewModel model) => SendAsync(
        HttpMethod.Post, "/api/Auth/forgot-password", new { email = model.Email.Trim() }, "forgot password");

    public Task<ApiResult> ResetPasswordAsync(ResetPasswordViewModel model) => SendAsync(
        HttpMethod.Post, "/api/Auth/reset-password", new
        {
            email = model.Email.Trim(), token = model.Token.Trim(),
            newPassword = model.NewPassword, confirmNewPassword = model.ConfirmNewPassword
        }, "reset password");

    private async Task<ApiResult> SendAsync(HttpMethod method, string path, object payload, string operation)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path)
            {
                Content = JsonContent.Create(payload)
            };
            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var message = GetSafeApiMessage(body) ?? (operation switch
                {
                    "register" => "Your account has been created. Check your email for the next step.",
                    "forgot password" => "If that email is registered, instructions will be sent shortly.",
                    "reset password" => "Your password has been updated.",
                    _ => "Request completed successfully."
                });
                return new ApiResult { Success = true, StatusCode = (int)response.StatusCode, Message = message, Data = body };
            }

            _logger.LogWarning("API {Operation} request failed with status {StatusCode}.", operation, (int)response.StatusCode);
            return new ApiResult
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                Message = MapError((int)response.StatusCode, body, operation)
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Unable to reach API during {Operation}.", operation);
            return new ApiResult { Success = false, StatusCode = 503,
                Message = "We couldn't connect to the service. Please try again shortly." };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "API request timed out during {Operation}.", operation);
            return new ApiResult { Success = false, StatusCode = 504,
                Message = "The service is taking too long to respond. Please try again." };
        }
    }

    private static string MapError(int statusCode, string body, string operation)
    {
        var apiMessage = GetSafeApiMessage(body)?.ToLowerInvariant() ?? string.Empty;
        if (statusCode == (int)HttpStatusCode.Unauthorized)
        {
            if (operation == "reset password") return "The password reset service is not accepting reset codes yet. Please request a new code later or contact the project team.";
            if (operation == "verify email") return "That verification token is invalid or has expired. Open the latest verification email and try again.";
            if (apiMessage.Contains("verify") || apiMessage.Contains("confirmed"))
                return "Please verify your email address before signing in.";
            if (operation == "login") return "Your email or password is incorrect.";
            return "Your session has expired. Please sign in and try again.";
        }
        if (statusCode == (int)HttpStatusCode.BadRequest)
        {
            if (operation == "reset password") return "We couldn't verify that reset code. Please check it and try again, or request a new one.";
            if (operation == "verify email") return "That verification token is invalid or has expired. Open the latest verification email and try again.";
            if (apiMessage.Contains("password") && apiMessage.Contains("match")) return "The passwords do not match.";
            if (apiMessage.Contains("token") && (apiMessage.Contains("invalid") || apiMessage.Contains("expired"))) return "That link or code is invalid or has expired. Request a new one and try again.";
            if (apiMessage.Contains("email") && apiMessage.Contains("exist")) return "An account with this email may already exist. Try signing in instead.";
            return "Please check the information you entered and try again.";
        }
        if (statusCode == (int)HttpStatusCode.Conflict) return "An account with these details may already exist. Please check and try again.";
        if (statusCode == (int)HttpStatusCode.NotFound) return operation == "reset password"
            ? "We couldn't find a matching account or reset request. Please request a new reset email."
            : "We couldn't find the requested account.";
        if (statusCode >= 500) return "Something went wrong on our end. Please try again later.";
        return operation == "login"
            ? "We couldn't sign you in. Please check your details and try again."
            : "We couldn't complete your request. Please try again.";
    }

    private static string? GetSafeApiMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return GetString(root, "message") ?? GetString(root, "Message") ?? GetString(root, "title");
        }
        catch (JsonException) { return null; }
    }

    private static JsonElement TryGet(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }
        return default;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        var value = TryGet(element, propertyName);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
