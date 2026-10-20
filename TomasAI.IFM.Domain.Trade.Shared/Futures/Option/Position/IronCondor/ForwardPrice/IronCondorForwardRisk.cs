using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

/// <summary>Versioned projected trade PnL and signed loss, with the exact entry-credit basis. Historical versions retain their meaning.</summary>
[MessagePackObject]
public sealed record IronCondorForwardRisk
{
    [Key(0)] public IronCondorForwardPrice IronCondorForwardPrice { get; init; } = new();
    [Key(1)] public decimal ForwardLoss { get; init; }
    [Key(2)] public decimal MaxLossLimit { get; init; }
    [Key(3)] public double ForwardLossRatio { get; init; }
    [Key(4)] public decimal ForwardLossHeadroom { get; init; }
    [Key(5)] public bool ExitRecommended { get; init; }
    [Key(6)] public Guid ForwardPriceCommandId { get; init; }
    [Key(7)] public string CalculationVersion { get; init; } = "IronCondor/SignedTotalForwardValue/v1";
    /// <summary>Gets the entry credit in points per complete strategy unit; absent in historical v1 payloads.</summary>
    [Key(8)] public decimal? EntryCreditPoints { get; init; }
    /// <summary>Gets projected whole-trade PnL in contract currency; absent in historical v1 payloads.</summary>
    [Key(9)] public decimal? ForwardPnl { get; init; }
    public const string EntryCreditCalculationVersion = "IronCondor/EntryCreditForwardPnl/v2";
}
