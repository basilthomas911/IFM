using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record LatestPriceRequest
{
    public required string Dataset { get; init; }
    public required string Symbol { get; init; }
    public DatabentoInputSymbology InputSymbology { get; init; } =
        DatabentoInputSymbology.RawSymbol;
    public LatestPricePolicy PricePolicy { get; init; } =
        LatestPricePolicy.LastTrade;
    public LatestPriceFreshnessPolicy FreshnessPolicy { get; init; } =
        LatestPriceFreshnessPolicy.NextObserved;
    public TimeSpan ReplayLookback { get; init; }
}
