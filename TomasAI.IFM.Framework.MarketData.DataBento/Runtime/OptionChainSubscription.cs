using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record OptionChainSubscription
{
    /// <summary>Allows the session manager to combine exact resolved expiries in one discovery connection.</summary>
    internal bool MultipleMaturities { get; init; }
    public required string Underlying { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public required IReadOnlyList<decimal> Strikes { get; init; }
    public OptionRightSelection Rights { get; init; } = OptionRightSelection.Both;
    public required IReadOnlyList<OptionContractDefinition> ResolvedContracts { get; init; }
    public MarketDataKinds DataKinds { get; init; } =
        MarketDataKinds.Quote | MarketDataKinds.Trade | MarketDataKinds.Statistics | MarketDataKinds.SessionVolume;
}
