using Microsoft.AspNetCore.Mvc;
using VaultEncryptionPOC.Models.Account;
using VaultEncryptionPOC.Services.Api;

namespace VaultEncryptionPOC.Controllers;

[Route("User")]
public class UserController : Controller
{
    private readonly IAuthenticationApiClient _authenticationApiClient;

    public UserController(IAuthenticationApiClient authenticationApiClient)
    {
        _authenticationApiClient = authenticationApiClient;
    }

    // Email links can open /User/Verify?token=... directly.
    [HttpGet("Verify")]
    public IActionResult Verify([FromQuery] string? token)
    {
        var cleanedToken = token?.Trim() ?? string.Empty;
        ViewBag.VerificationAttempted = string.IsNullOrWhiteSpace(cleanedToken);
        return View(new VerifyEmailViewModel { Token = cleanedToken });
    }

    [HttpPost("Verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(VerifyEmailViewModel model)
    {
        ViewBag.VerificationAttempted = true;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authenticationApiClient.ValidateSignupTokenAsync(model.Token.Trim());
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["SuccessMessage"] = "Your email has been verified. You can now sign in.";
        return RedirectToAction("Login", "Account");
    }
}
