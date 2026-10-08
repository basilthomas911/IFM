using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

/// <summary>Identifies the observed market price basis; a quote is never an executed source trade.</summary>
public enum OptionMarketPriceBasis { Trade = 1, QuoteMidpoint = 2 }

/// <summary>One identified option market observation used for live position marking and UI notification.</summary>
/// <param name="UnderlyingContractId">The exact provider-qualified underlying futures contract.</param>
/// <param name="PriceBasis">Whether the price is a source trade or a two-sided quote midpoint.</param>
/// <param name="OptionTickData">The observed prices and any available sensitivities; unavailable Greeks are explicitly marked.</param>
/// <param name="SourceSequence">The provider instrument sequence shared by quote and trade ordering.</param>
/// <param name="EventAtUtc">The UTC market timestamp of this observation.</param>
[MessagePackObject]
public sealed record OptionMarketPriceObservation(
    [property: Key(0)] string UnderlyingContractId,
    [property: Key(1)] OptionMarketPriceBasis PriceBasis,
    [property: Key(2)] FuturesOptionTickDataV2ReadModel OptionTickData,
    [property: Key(3)] long SourceSequence,
    [property: Key(4)] DateTime EventAtUtc);
