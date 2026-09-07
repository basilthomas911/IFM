using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

/// <summary>Preserves the original Portfolio canonical JSON, including decimal scale, in workflow event storage.</summary>
public sealed class SelectionPortfolioSnapshotJsonConverter : JsonConverter<PortfolioFundStrategySnapshot>
{
    public override void WriteJson(JsonWriter writer, PortfolioFundStrategySnapshot? value, JsonSerializer serializer)
        => writer.WriteRawValue(System.Text.Json.JsonSerializer.Serialize(value));

    public override PortfolioFundStrategySnapshot? ReadJson(JsonReader reader, Type objectType,
        PortfolioFundStrategySnapshot? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var floats = reader.FloatParseHandling;
        var dates = reader.DateParseHandling;
        try
        {
            reader.FloatParseHandling = FloatParseHandling.Decimal;
            reader.DateParseHandling = DateParseHandling.None;
            var json = JToken.Load(reader).ToString(Formatting.None);
            return System.Text.Json.JsonSerializer.Deserialize<PortfolioFundStrategySnapshot>(json);
        }
        finally
        {
            reader.FloatParseHandling = floats;
            reader.DateParseHandling = dates;
        }
    }
}
