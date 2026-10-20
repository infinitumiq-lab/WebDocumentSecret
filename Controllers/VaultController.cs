using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using VaultEncryptionPOC.Services.Api;

namespace VaultEncryptionPOC.Controllers;

public class VaultController : Controller
{
    private readonly IDocumentApiClient _documentApiClient;
    public VaultController(IDocumentApiClient documentApiClient) => _documentApiClient = documentApiClient;

    public sealed class SaveEncryptedRequest
    {
        public string EncryptedEnvelope { get; set; } = string.Empty;
        public string? DocumentTypeId { get; set; }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveEncryptedRequest request)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("AccessToken")))
            return Unauthorized(new { success = false, message = "Your session has expired. Please sign in again." });
        if (request is null || string.IsNullOrWhiteSpace(request.EncryptedEnvelope))
            return BadRequest(new { success = false, message = "The encrypted document is empty." });
        var result = await _documentApiClient.SaveEncryptedDocumentAsync(request.EncryptedEnvelope, request.DocumentTypeId);
        if (!result.Success) return StatusCode(result.StatusCode is >= 400 and <= 599 ? result.StatusCode : 502, new { success = false, message = result.Message });
        return Ok(new { success = true, message = result.Message, recordId = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Retrieve(string id)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("AccessToken")))
            return Unauthorized(new { success = false, message = "Your session has expired. Please sign in again." });
        var result = await _documentApiClient.GetEncryptedDocumentAsync(id);
        if (!result.Success || result.Data is null)
            return StatusCode(result.StatusCode is >= 400 and <= 599 ? result.StatusCode : 502, new { success = false, message = result.Message });
        return Ok(new { success = true, message = result.Message, recordId = result.Data.RecordId, envelope = result.Data.Envelope, documentTypeId = result.Data.DocumentTypeId });
    }

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
