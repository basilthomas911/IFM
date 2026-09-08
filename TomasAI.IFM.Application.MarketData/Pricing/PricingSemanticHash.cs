using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Versioned semantic JSON hashing, independent of MessagePack transport and decimal storage scale.</summary>
public static class PricingSemanticHash
{
    public static string Compute<T>(T value)
    {
        using var document = JsonSerializer.SerializeToDocument(value);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) Write(document.RootElement, writer);
        return Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))));
    }

    static void Write(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); Write(property.Value, writer); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(item, writer);
                writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                var raw = value.GetRawText();
                if (!raw.Contains('e', StringComparison.OrdinalIgnoreCase) && value.TryGetDecimal(out var number))
                    writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
                else writer.WriteRawValue(value.GetDouble().ToString("R", CultureInfo.InvariantCulture));
                break;
            default: value.WriteTo(writer); break;
        }
    }
}
