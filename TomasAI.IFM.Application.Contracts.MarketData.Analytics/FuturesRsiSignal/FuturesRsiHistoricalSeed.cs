using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
[MessagePackObject]
public sealed record FuturesRsiHistoricalSeed(
 [property: Key(0)] int RequestedPeriods, [property: Key(1)] DateTimeOffset AsOfUtc,
 [property: Key(2)] FuturesTradeSessionBarReadModel[] Observations, [property: Key(3)] string SourceReason);
