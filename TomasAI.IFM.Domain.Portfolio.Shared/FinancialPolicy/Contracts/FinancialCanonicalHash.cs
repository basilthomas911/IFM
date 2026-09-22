using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Semantic hashing is independent of MessagePack transport encoding and current process culture.</summary>
public static class FinancialCanonicalHash
{
    /// <summary>One canonical requirement-vector identity shared by Risk, financial admission and clients.</summary>
    public static string Requirements(CapacityRequirements value) => Compute(value with
    {
        ContentHash = string.Empty,
        Exposures = value.Exposures.OrderBy(x => x.ScopeKind).ThenBy(x => x.ScopeKey, StringComparer.Ordinal)
            .ThenBy(x => x.Measure).ThenBy(x => x.Unit).ToArray()
    });
    static readonly JsonSerializerOptions Options=new() { Converters={ new CanonicalUtcDateTimeConverter() } };
    public static string Request<T>(IFinancialRequest<T> request) => Compute(new
    {
        request.PortfolioId, request.OperationId, request.ExpectedFinancialRevision, request.RequestedAtUtc,
        request.ExpiresAtUtc, request.Body, Principal = request.Access.Principal
    });

    public static string Compute<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value,Options);
        var text = new StringBuilder(); Write(element, text);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    // MessagePack timestamps deserialize as UTC, including optional/default DateTime values.
    // Semantic identity must not depend on the DateTime.Kind of an otherwise identical instant.
    sealed class CanonicalUtcDateTimeConverter:JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>reader.GetDateTime();
        public override void Write(Utf8JsonWriter writer,DateTime value,JsonSerializerOptions options)
            =>writer.WriteStringValue(value.Kind==DateTimeKind.Local?value.ToUniversalTime():DateTime.SpecifyKind(value,DateTimeKind.Utc));
    }

    static void Write(JsonElement value, StringBuilder output)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{'); var first = true;
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first) output.Append(','); first = false;
                    output.Append(JsonSerializer.Serialize(property.Name)).Append(':'); Write(property.Value, output);
                }
                output.Append('}'); break;
            case JsonValueKind.Array:
                output.Append('['); var initial = true;
                foreach (var item in value.EnumerateArray()) { if (!initial) output.Append(','); initial = false; Write(item, output); }
                output.Append(']'); break;
            case JsonValueKind.Number:
                output.Append(value.GetDecimal().ToString("G29", CultureInfo.InvariantCulture)); break;
            default: output.Append(value.GetRawText()); break;
        }
    }
}
