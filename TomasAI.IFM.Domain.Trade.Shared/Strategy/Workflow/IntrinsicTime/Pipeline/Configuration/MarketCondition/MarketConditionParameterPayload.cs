using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

public static class MarketConditionParameterPayload
{
    static readonly JsonSerializerOptions Options = CreateOptions();
    public static string Serialize(MarketConditionParameterSet parameterSet)
    {
        ArgumentNullException.ThrowIfNull(parameterSet);
        return JsonSerializer.Serialize(parameterSet, Options);
    }
    public static string ComputeSha256(MarketConditionParameterSet parameterSet) => ComputeSha256(Serialize(parameterSet));
    public static string ComputeSha256(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
    }

    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = false };
        options.Converters.Add(new CanonicalDecimalConverter());
        return options;
    }

    sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}
