using Microsoft.AspNetCore.Mvc;
using VaultEncryptionPOC.Services.Api;

namespace VaultEncryptionPOC.Controllers;

public class VaultController : Controller
{
    private readonly IDocumentApiClient _documentApiClient;
    public VaultController(IDocumentApiClient documentApiClient) => _documentApiClient = documentApiClient;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("AccessToken")))
        {
            TempData["InfoMessage"] = "Please sign in to access your vault.";
            return RedirectToAction("Login", "Account");
        }

        var result = await _documentApiClient.GetFormSchemasAsync();
        if (!result.Success && result.StatusCode == 401)
        {
            HttpContext.Session.Clear();
            TempData["InfoMessage"] = result.Message;
            return RedirectToAction("Login", "Account");
        }

        ViewBag.ApiMessage = result.Message;
        ViewBag.ApiSuccess = result.Success;
        return View(result.Data ?? Array.Empty<VaultEncryptionPOC.Models.Vault.DocumentTypeViewModel>());
    }
}
