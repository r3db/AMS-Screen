using System;
using System.Text.Json;

namespace BambuDashboard;

internal static class JsonElementExtensions
{
    internal static string? GetStringOrNull(this JsonElement element, string propertyName, string? defaultValue = null)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String) return prop.GetString() ?? defaultValue;
            if (prop.ValueKind == JsonValueKind.Number) return prop.GetRawText();
        }

        return defaultValue;
    }

    internal static int? GetInt32OrNull(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number) return prop.GetInt32();
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var val)) return val;
        }

        return null;
    }

    internal static float GetSingleOrDefault(this JsonElement element, string propertyName, float defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number) return prop.GetSingle();
            if (prop.ValueKind == JsonValueKind.String && float.TryParse(prop.GetString(), out var val)) return val;
        }

        return defaultValue;
    }

    internal static double GetDoubleOrDefault(this JsonElement element, string propertyName, double defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number) return prop.GetDouble();
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var val)) return val;
        }

        return defaultValue;
    }
}