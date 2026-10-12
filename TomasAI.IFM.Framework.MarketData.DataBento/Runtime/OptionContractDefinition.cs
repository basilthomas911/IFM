using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record OptionContractDefinition
{
    public required string Dataset { get; init; }
    public required string RawSymbol { get; init; }
    public required string Ticker { get; init; }
    public required string Underlying { get; init; }
    public required InstrumentKey Instrument { get; init; }
    public required OptionRightSelection Right { get; init; }
    public required decimal StrikePrice { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public ulong? ExpirationTimestampNanoseconds { get; init; }
    public ulong? ActivationTimestampNanoseconds { get; init; }
    public long? MinimumPriceIncrement { get; init; }
    public int? ContractMultiplier { get; init; }
}
