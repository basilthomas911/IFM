using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

/// <summary>Published engineering policy. Emulator figures are explicit configuration, never broker margin quotations.</summary>
public sealed record RiskParameterSet
{
    [JsonRequired] public short SchemaVersion { get; init; } = 1;
    [JsonRequired] public Guid ParameterSetId { get; init; }
    [JsonRequired] public int Version { get; init; } = 1;
    [JsonRequired] public TimeFrameType TargetHorizon { get; init; }
    [JsonRequired] public string Root { get; init; } = "ES";
    [JsonRequired] public string Currency { get; init; } = "USD";
    [JsonRequired] public string Environment { get; init; } = "Emulator";
    [JsonRequired] public int MaximumUnits { get; init; } = 10;
    [JsonRequired] public decimal PerTradeRiskFraction { get; init; } = .01m;
    [JsonRequired] public string RiskCapitalBasis { get; init; } = "AvailableSettledCash";
    [JsonRequired] public int MarginMethodVersion { get; init; } = 1;
    [JsonRequired] public decimal MarginPerGrossContract { get; init; } = 25000m;
    [JsonRequired] public decimal FeePerGrossContract { get; init; } = 5m;
    [JsonRequired] public decimal VariationReservePerGrossContract { get; init; } = 1000m;
    [JsonRequired] public decimal IncrementalLossReserve { get; init; }

    static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        Converters={new TradeSelectionPolicy.CanonicalDecimalConverter()}
    };

    public RiskSizingPolicy Sizing() => new(TargetHorizon,MaximumUnits,PerTradeRiskFraction);
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this,Options); }
    public string Hash() => TradeSelectionPolicy.HashJson(Serialize());
    public static RiskParameterSet Read(string json)
    {
        TradeSelectionPolicy.CheckJson(json);
        var value=JsonSerializer.Deserialize<RiskParameterSet>(json,Options) ?? throw new ArgumentException("RM.CONFIG.MISSING");
        value.Validate(); return value;
    }
    public void Validate()
    {
        if (SchemaVersion!=1 || ParameterSetId==Guid.Empty || Version<=0
            || TargetHorizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly)
            || Root!="ES" || Currency!="USD" || Environment!="Emulator" || RiskCapitalBasis!="AvailableSettledCash"
            || MaximumUnits is <=0 or >100 || PerTradeRiskFraction is <=0 or >1 || MarginMethodVersion!=1
            || MarginPerGrossContract is <=0 or >1000000 || FeePerGrossContract is <0 or >1000
            || VariationReservePerGrossContract is <0 or >1000000 || IncrementalLossReserve is <0 or >1000000)
            throw new ArgumentException("RM.CONFIG.INVALID");
    }
    /// <summary>Three stable draft identities, one per horizon. Creation does not publish, assign or activate them.</summary>
    public static RiskParameterSet Default(TimeFrameType horizon) => new()
    {
        TargetHorizon=horizon,
        ParameterSetId=horizon switch
        {
            TimeFrameType.Daily => Guid.Parse("597ecfc1-23b3-4fb2-8ed4-49172cb58f01"),
            TimeFrameType.Weekly => Guid.Parse("597ecfc1-23b3-4fb2-8ed4-49172cb58f02"),
            TimeFrameType.Monthly => Guid.Parse("597ecfc1-23b3-4fb2-8ed4-49172cb58f03"),
            _ => throw new ArgumentOutOfRangeException(nameof(horizon))
        }
    };
}
