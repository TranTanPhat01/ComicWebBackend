using ComicWeb.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComicWeb.Persistence.Logging;

public sealed class AuditDetailsSerializer : IAuditDetailsSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] _bannedKeys = new[]
    {
        "password", "passwordhash", "accesstoken", "refreshtoken", 
        "token", "authorization", "signingkey", "connectionstring", 
        "content", "rawcontent"
    };

    public string? Serialize(object? details)
    {
        if (details == null) return null;

        try
        {
            var element = JsonSerializer.SerializeToElement(details, _options);
            var sanitizedElement = SanitizeElement(element);
            
            var json = JsonSerializer.Serialize(sanitizedElement, _options);
            
            // Limit to 8KB (8192 characters)
            if (json.Length > 8192)
            {
                return json.Substring(0, 8192) + "... [TRUNCATED]";
            }
            return json;
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"Serialization failed: {ex.Message}\"}}";
        }
    }

    private static object? SanitizeElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                {
                    if (IsBanned(prop.Name))
                    {
                        dict[prop.Name] = "[REDACTED]";
                    }
                    else
                    {
                        dict[prop.Name] = SanitizeElement(prop.Value);
                    }
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var val in element.EnumerateArray())
                {
                    list.Add(SanitizeElement(val));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                if (element.TryGetDouble(out var d)) return d;
                return element.GetRawText();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            default:
                return null;
        }
    }

    private static bool IsBanned(string name)
    {
        var lower = name.ToLowerInvariant();
        foreach (var banned in _bannedKeys)
        {
            if (lower.Contains(banned)) return true;
        }
        return false;
    }
}
