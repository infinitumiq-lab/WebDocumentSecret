using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using VaultEncryptionPOC.Models.Vault;

namespace VaultEncryptionPOC.Services.Api;

/// <summary>Retrieves the authenticated user's dynamic form definitions from FormSchemas.</summary>
public class DocumentApiClient : IDocumentApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DocumentApiClient> _logger;

    public DocumentApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, ILogger<DocumentApiClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>> GetFormSchemasAsync()
    {
        var token = _httpContextAccessor.HttpContext?.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
            return Failure((int)HttpStatusCode.Unauthorized, "Please sign in to load your document forms.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/FormSchemas");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FormSchemas request failed with status {StatusCode}.", (int)response.StatusCode);
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
                    HttpStatusCode.Forbidden => "Your account is not authorized to view these forms.",
                    HttpStatusCode.NotFound => "The FormSchemas endpoint was not found on the API.",
                    _ when (int)response.StatusCode >= 500 => "The form service is temporarily unavailable. Please try again later.",
                    _ => "We couldn't load the document forms. Please try again."
                };
                return Failure((int)response.StatusCode, message);
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var root = json.RootElement;
            var flag = FindProperty(root, "flag");
            if (flag.ValueKind == JsonValueKind.False)
                return Failure((int)response.StatusCode, GetString(root, "message") ?? "The form service could not return the schemas.");

            var data = FindProperty(root, "data");
            if (data.ValueKind != JsonValueKind.Array)
                return Failure(502, "The form service returned data in an unexpected format.");

            var schemas = new List<DocumentTypeViewModel>();
            foreach (var item in data.EnumerateArray())
            {
                var id = GetString(item, "documentTypeId");
                var name = GetString(item, "typeName");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogWarning("A FormSchemas item was skipped because it had no documentTypeId or typeName.");
                    continue;
                }

                var schema = new DocumentTypeViewModel { DocumentTypeId = id, TypeName = name };
                var fields = FindProperty(item, "fields");
                if (fields.ValueKind == JsonValueKind.Array)
                {
                    foreach (var field in fields.EnumerateArray())
                    {
                        var keyName = GetString(field, "keyName");
                        if (string.IsNullOrWhiteSpace(keyName)) continue;
                        var inputType = (GetString(field, "inputType") ?? "text").Trim().ToLowerInvariant();
                        var options = new List<string>();
                        var optionsElement = FindProperty(field, "options");
                        if (optionsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var option in optionsElement.EnumerateArray())
                                if (option.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(option.GetString()))
                                    options.Add(option.GetString()!);
                        }

                        schema.Fields.Add(new DocumentFieldViewModel
                        {
                            KeyName = keyName,
                            InputType = inputType,
                            Options = options
                        });
                    }
                }
                schemas.Add(schema);
            }

            return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Message = schemas.Count == 0 ? "No document forms are currently available." : "Document forms loaded successfully.",
                Data = schemas.OrderBy(x => x.TypeName, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Unable to reach the API while retrieving FormSchemas.");
            return Failure(503, "We couldn't connect to the form service. Please try again shortly.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "FormSchemas request timed out.");
            return Failure(504, "The form service is taking too long to respond. Please try again.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "The FormSchemas response could not be parsed.");
            return Failure(502, "The form service returned data in an unexpected format.");
        }
    }

    public async Task<ApiResultWithData<string>> SaveEncryptedDocumentAsync(string encryptedEnvelope, string? documentTypeId)
    {
        if (string.IsNullOrWhiteSpace(encryptedEnvelope))
            return new() { Success = false, StatusCode = 400, Message = "The encrypted document is empty." };

        var token = _httpContextAccessor.HttpContext?.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
            return new() { Success = false, StatusCode = 401, Message = "Please sign in again before saving." };

        Guid? parsedTypeId = Guid.TryParse(documentTypeId, out var parsed) ? parsed : null;
        var payload = new CreateEncryptedDocumentRequest { EncryptedEnvelope = encryptedEnvelope, DocumentTypeId = parsedTypeId };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/EncryptedDocuments");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request);
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var root = json.RootElement;
            var flag = FindProperty(root, "flag");
            var data = FindProperty(root, "data");
            var recordId = GetString(data, "recordId");
            var message = GetString(root, "message") ?? (response.IsSuccessStatusCode ? "Document saved." : "The document could not be saved.");
            if (!response.IsSuccessStatusCode || flag.ValueKind == JsonValueKind.False || string.IsNullOrWhiteSpace(recordId))
                return new() { Success = false, StatusCode = (int)response.StatusCode, Message = message };
            return new() { Success = true, StatusCode = (int)response.StatusCode, Message = message, Data = recordId };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Unable to reach the API while saving an encrypted document.");
            return new() { Success = false, StatusCode = 503, Message = "We couldn't reach the secure storage service. Please try again." };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Encrypted document save timed out.");
            return new() { Success = false, StatusCode = 504, Message = "Saving took too long. Please try again." };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "The encrypted document save response could not be parsed.");
            return new() { Success = false, StatusCode = 502, Message = "The storage service returned an unexpected response." };
        }
    }

    public async Task<ApiResultWithData<EncryptedDocumentRecord>> GetEncryptedDocumentAsync(string recordId)
    {
        var token = _httpContextAccessor.HttpContext?.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
            return new() { Success = false, StatusCode = 401, Message = "Please sign in again to retrieve this document." };
        if (!Guid.TryParse(recordId, out var id))
            return new() { Success = false, StatusCode = 400, Message = "The document record ID is invalid." };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/EncryptedDocuments/{id:D}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request);
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var root = json.RootElement;
            var flag = FindProperty(root, "flag");
            var data = FindProperty(root, "data");
            var envelope = GetString(data, "envelope");
            var returnedId = GetString(data, "recordId");
            var message = GetString(root, "message") ?? "The document could not be retrieved.";
            if (!response.IsSuccessStatusCode || flag.ValueKind == JsonValueKind.False || string.IsNullOrWhiteSpace(envelope))
                return new() { Success = false, StatusCode = (int)response.StatusCode, Message = message };
            var createdAtText = GetString(data, "createdAt");
            DateTime? createdAt = DateTime.TryParse(createdAtText, out var parsedDate) ? parsedDate : null;
            Guid? typeId = Guid.TryParse(GetString(data, "documentTypeId"), out var parsedType) ? parsedType : null;
            return new()
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Message = string.IsNullOrWhiteSpace(message) ? "Encrypted document retrieved." : message,
                Data = new EncryptedDocumentRecord { RecordId = returnedId ?? id.ToString(), Envelope = envelope, CreatedAt = createdAt, DocumentTypeId = typeId }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Unable to reach the API while retrieving an encrypted document.");
            return new() { Success = false, StatusCode = 503, Message = "We couldn't reach secure storage. Please try again." };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Encrypted document retrieval timed out.");
            return new() { Success = false, StatusCode = 504, Message = "Retrieving took too long. Please try again." };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "The encrypted document retrieval response could not be parsed.");
            return new() { Success = false, StatusCode = 502, Message = "The storage service returned an unexpected response." };
        }
    }

    private static ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>> Failure(int statusCode, string message) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Message = message,
        Data = Array.Empty<DocumentTypeViewModel>()
    };

    private static JsonElement FindProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return default;
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return default;
    }

    private static string? GetString(JsonElement element, string name)
    {
        var value = FindProperty(element, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
