

namespace TomasAI.IFM.Shared.MarketData.Definitions;

public sealed record ContractDetail
{
    public required string Dataset { get; init; }
    public required string RawSymbol { get; init; }
    public required string Ticker { get; init; }
    public required string Underlying { get; init; }
    public required InstrumentKey Instrument { get; init; }
    public required ContractKind ContractKind { get; init; }
    public ulong RawInstrumentId { get; init; }
    public uint UnderlyingInstrumentId { get; init; }
    public int? ContractMultiplier { get; init; }
    public long? StrikePrice { get; init; }
    public long? MinimumPriceIncrement { get; init; }
    public long? MinimumPriceIncrementAmount { get; init; }
    public ulong? ExpirationTimestampNanoseconds { get; init; }
    public ulong? ActivationTimestampNanoseconds { get; init; }
    public DateOnly? MaturityDate { get; init; }
    public byte? MaturityWeek { get; init; }
    public required string Currency { get; init; }
    public required string SettlementCurrency { get; init; }
    public required string Exchange { get; init; }
    public required string SecurityType { get; init; }
    public required string Cfi { get; init; }
    public required string UnitOfMeasure { get; init; }
}
