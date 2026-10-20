using Microsoft.AspNetCore.Mvc;
using VaultEncryptionPOC.Models.Account;
using VaultEncryptionPOC.Services.Api;

namespace VaultEncryptionPOC.Controllers;

public class AccountController : Controller
{
    private readonly IAuthenticationApiClient _authenticationApiClient;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthenticationApiClient authenticationApiClient, ILogger<AccountController> logger)
    {
        _authenticationApiClient = authenticationApiClient;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _authenticationApiClient.RegisterAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["SuccessMessage"] = "Your account was created. Open the verification link in your email before signing in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _authenticationApiClient.LoginAsync(model);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetString("AccessToken", result.AccessToken);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken)) HttpContext.Session.SetString("RefreshToken", result.RefreshToken);
        if (!string.IsNullOrWhiteSpace(result.Email)) HttpContext.Session.SetString("UserEmail", result.Email);
        if (!string.IsNullOrWhiteSpace(result.Role)) HttpContext.Session.SetString("UserRole", result.Role);

        TempData["SuccessMessage"] = "You're signed in.";
        return RedirectToAction("Dashboard", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _authenticationApiClient.ForgotPasswordAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        // Keep the same confirmation whether or not the email is registered.
        TempData["ResetEmail"] = model.Email.Trim();
        TempData["SuccessMessage"] = "If an account exists for that email, a reset code has been sent. Check your inbox and spam folder.";
        return RedirectToAction(nameof(ResetPassword), new { email = model.Email.Trim() });
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        return View(new ResetPasswordViewModel { Email = email ?? string.Empty, Token = token ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _authenticationApiClient.ResetPasswordAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["SuccessMessage"] = "Your password has been updated. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["SuccessMessage"] = "You have been signed out.";
        return RedirectToAction("Index", "Home");
    }
}
