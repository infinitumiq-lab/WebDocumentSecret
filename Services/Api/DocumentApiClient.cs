using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using VaultEncryptionPOC.Models.Vault;

namespace VaultEncryptionPOC.Services.Api;

/// <summary>
/// Builds a temporary form preview from the currently working Documents endpoint.
/// Document records are used only to discover document-type IDs and attribute names;
/// saved values and filenames are deliberately not rendered in the form.
/// </summary>
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
        {
            return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Message = "Please sign in to load your document types."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/Documents");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Documents request failed with status {StatusCode}.", (int)response.StatusCode);
                var statusCode = (int)response.StatusCode;
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
                    HttpStatusCode.Forbidden => "Your account is not authorized to view documents.",
                    HttpStatusCode.NotFound => "The Documents endpoint was not found on the API.",
                    _ when statusCode >= 500 => "The document service is temporarily unavailable. Please try again later.",
                    _ => "We couldn't load document types right now. Please try again later."
                };

                return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
                {
                    Success = false, StatusCode = (int)response.StatusCode, Message = message
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var data = FindProperty(json.RootElement, "data");
            if (data.ValueKind == JsonValueKind.Undefined)
            {
                data = json.RootElement;
            }

            if (data.ValueKind != JsonValueKind.Array)
            {
                return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
                {
                    Success = false, StatusCode = 502,
                    Message = "The document service returned data in an unexpected format."
                };
            }

            // Aggregate attribute keys by document type. Never copy attribute VALUES into the UI.
            var grouped = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in data.EnumerateArray())
            {
                var typeId = GetString(document, "documentTypeId");
                if (string.IsNullOrWhiteSpace(typeId)) continue;

                if (!grouped.TryGetValue(typeId, out var fields))
                {
                    fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    grouped[typeId] = fields;
                }

                var attributes = FindProperty(document, "dynamicAttributes");
                if (attributes.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in attributes.EnumerateObject())
                    {
                        if (!string.IsNullOrWhiteSpace(property.Name)) fields.Add(property.Name);
                    }
                }
            }

            var types = grouped
                .OrderBy(pair => GetTypeName(pair.Key), StringComparer.OrdinalIgnoreCase)
                .Select(pair => new DocumentTypeViewModel
                {
                    DocumentTypeId = pair.Key,
                    TypeName = GetTypeName(pair.Key),
                    Fields = pair.Value
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .Select(name => new DocumentFieldViewModel
                        {
                            KeyName = name,
                            InputType = InferInputType(name)
                        })
                        .ToList()
                })
                .ToList();

            return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Message = types.Count == 0
                    ? "The Documents endpoint is working, but it returned no document attributes to use for the form preview yet."
                    : "Document types and fields were generated from the Documents response. This is a form preview; saving is not connected yet.",
                Data = types
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Unable to reach API while retrieving documents.");
            return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
            { Success = false, StatusCode = 503, Message = "We couldn't connect to the service. Please try again shortly." };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Documents request timed out.");
            return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
            { Success = false, StatusCode = 504, Message = "The service is taking too long to respond. Please try again." };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Documents response had an unexpected format.");
            return new ApiResultWithData<IReadOnlyList<DocumentTypeViewModel>>
            { Success = false, StatusCode = 502, Message = "The service returned document data in an unexpected format." };
        }
    }

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

    private static string GetTypeName(string typeId)
    {
        // Friendly labels for the known sample IDs; unknown IDs still get a readable label.
        var lower = typeId.ToLowerInvariant();
        if (lower.Contains("invoice")) return "Invoice";
        if (lower.Contains("passport")) return "Passport / Identity Document";
        if (lower.Contains("contract")) return "Contract";
        if (lower.Contains("tax")) return "Tax Document";
        if (lower.Contains("bank")) return "Bank Statement";

        var readable = typeId.Replace('_', ' ').Replace('-', ' ');
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(readable);
    }

    private static string InferInputType(string keyName)
    {
        var key = keyName.ToLowerInvariant();
        if (key.Contains("date") || key.EndsWith("year", StringComparison.Ordinal)) return "date";
        if (key.Contains("amount") || key.Contains("salary") || key.Contains("size")) return "number";
        return "text";
    }
}
