using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private async Task<(JsonElement? Payload, IFormFileCollection Files)> ReadQuotePayloadAsync(
        CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            var payload = ExtractPayloadFromForm(form);
            return (payload, form.Files);
        }

        if (Request.Body == null)
        {
            return (null, new FormFileCollection());
        }

        using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        return (document.RootElement.Clone(), new FormFileCollection());
    }

    private static JsonElement? ExtractPayloadFromForm(IFormCollection form)
    {
        if (form.TryGetValue("quoteData", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            return ParseJson(raw.ToString());
        }

        if (form.Count == 0)
        {
            return null;
        }

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in form.Keys)
        {
            if (string.Equals(key, "quoteData", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = form[key].ToString();
            if (string.Equals(key, "lineItems", StringComparison.OrdinalIgnoreCase) && TryParseJson(value, out var lineItems))
            {
                payload[key] = lineItems;
                continue;
            }

            payload[key] = value;
        }

        var json = JsonSerializer.Serialize(payload);
        return ParseJson(json);
    }

    private static JsonElement? ParseJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseJson(string raw, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            element = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<StoredFile>> SaveAttachmentsAsync(
        IEnumerable<IFormFile> files,
        long? rfqId,
        CancellationToken cancellationToken)
    {
        var stored = new List<StoredFile>();
        if (files == null)
        {
            return stored;
        }

        var uploadDir = ResolveUploadDirectory(rfqId);
        foreach (var file in files)
        {
            if (file == null || file.Length <= 0)
            {
                continue;
            }

            var originalName = DecodeFileName(file.FileName);
            var extension = Path.GetExtension(originalName);
            var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, storedName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            stored.Add(new StoredFile(
                originalName,
                storedName,
                filePath,
                file.ContentType,
                file.Length));
        }

        return stored;
    }
}
