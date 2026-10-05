using Microsoft.AspNetCore.Mvc;
using VaultEncryptionPOC.Models;
using VaultEncryptionPOC.Services;


namespace VaultEncryptionPOC.Controllers
{
    public class VaultController : Controller
    {
        private readonly EncryptionService _encryptionService;

        public VaultController(EncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }


        // this displays the form to the user, allowing them to input their data for encryption
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }


        // this handles the form submission, encrypts the data, and redirects to the Result action to display the encrypted data
        [HttpPost]
        public IActionResult Encrypt(FormModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            // extracts the data from the model and encrypts it using the EncryptionService
            string encryptedData = _encryptionService.Encrypt(model);

            TempData["EncryptedData"] = encryptedData;

            return RedirectToAction("Result");
        }

        [HttpGet]
        public IActionResult Result()
        {
            string? encryptedData = TempData["EncryptedData"] as string;

            if (string.IsNullOrEmpty(encryptedData))
            {
                return RedirectToAction("Index");
            }

            return View(model: encryptedData);
        }

        [HttpPost]
        public IActionResult Decrypt(string encryptedData)
        {

            FormModel decryptedData = _encryptionService.Decrypt<FormModel>(encryptedData);

            return View("Decrypted", decryptedData);
        }
    }
}
