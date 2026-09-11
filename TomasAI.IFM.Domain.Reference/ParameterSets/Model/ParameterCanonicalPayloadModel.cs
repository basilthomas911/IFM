using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Bounded, deterministic JSON canonicalization; does not change array order or decimal precision.</summary>
public static class ParameterCanonicalPayloadModel
{
    public const int MaximumBytes = 1_048_576;
    public static string Canonicalize(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaximumBytes)
            throw new ArgumentException("PARAM.PAYLOAD_TOO_LARGE");
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("PARAM.OBJECT_REQUIRED");
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) Write(writer, document.RootElement);
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
    public static string Hash(string payload) => Convert.ToHexStringLower(
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Canonicalize(payload))));
    static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = element.EnumerateObject().ToArray();
                if (properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                    throw new ArgumentException("PARAM.DUPLICATE_PROPERTY");
                writer.WriteStartObject();
                foreach (var p in properties.OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(p.Name); Write(writer, p.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                // Preserve exact JSON number spelling. This codec does not round through double/decimal.
                writer.WriteRawValue(element.GetRawText()); break;
            default: element.WriteTo(writer); break;
        }
    }
}
