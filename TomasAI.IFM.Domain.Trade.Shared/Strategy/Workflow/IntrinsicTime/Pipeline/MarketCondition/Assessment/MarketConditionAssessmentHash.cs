using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

public static class MarketConditionAssessmentHash
{
    static readonly JsonSerializerOptions Options = CreateOptions();
    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CanonicalDecimalConverter());
        return options;
    }
    sealed class CanonicalDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => writer.WriteRawValue(value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
    }
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static string Compute<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value))));
    public static string Parameters(MarketConditionAssessmentParameterSet p)
    {
        ArgumentNullException.ThrowIfNull(p);
        p.Validate();
        return Compute(p with { Sources = p.Sources });
    }
}
