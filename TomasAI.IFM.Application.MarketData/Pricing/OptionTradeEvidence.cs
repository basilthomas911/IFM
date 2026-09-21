using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Exact source identity. Native nanoseconds are retained without DateTime rounding.</summary>
[MessagePackObject]
public sealed record OptionTradeSource(
    [property: Key(0)] string Dataset, [property: Key(1)] ushort PublisherId,
    [property: Key(2)] uint InstrumentId, [property: Key(3)] string ContractId,
    [property: Key(4)] DateOnly ValueDate, [property: Key(5)] decimal Price,
    [property: Key(6)] uint Size, [property: Key(7)] long Sequence,
    [property: Key(8)] long EventNanoseconds, [property: Key(9)] long ReceiveNanoseconds,
    [property: Key(10)] Guid GenerationId, [property: Key(11)] string RawSymbol)
{
    [IgnoreMember] public string Identity => PricingSemanticHash.Compute(new
        { Dataset, PublisherId, InstrumentId, ContractId, ValueDate, Sequence, EventNanoseconds });
    // Retransmission may have a different receive time/generation but may not alter economic source data.
    [IgnoreMember] public string SourceDigest => PricingSemanticHash.Compute(new
        { Identity, Price, Size, RawSymbol });
}

[MessagePackObject]
public sealed record OptionTradeEvidence(
    [property: Key(0)] OptionTradeSource Source,
    [property: Key(1)] OptionPricingContext? Context,
    [property: Key(2)] OptionPricingQuote? Underlying,
    [property: Key(3)] DateTimeOffset CalculatedAtUtc,
    [property: Key(4)] OptionPricingValue? Greeks,
    [property: Key(5)] OptionPricingFailure? Failure)
{
    [Key(6)] public string PriceBasis { get; init; } = "Trade";

    public void Validate()
    {
        if (Source is null || string.IsNullOrWhiteSpace(Source.Dataset) || Source.Dataset.Length > 64
            || string.IsNullOrWhiteSpace(Source.ContractId) || Source.ContractId.Length > 128
            || Source.PublisherId == 0 || Source.InstrumentId == 0 || Source.ValueDate == default
            || string.IsNullOrWhiteSpace(Source.RawSymbol) || Source.RawSymbol.Length > 256
            || Source.GenerationId == Guid.Empty || CalculatedAtUtc.Offset != TimeSpan.Zero
            || PriceBasis != "Trade" || (Greeks is null) == (Failure is null))
            throw new ArgumentException("An identified source and exactly one full-Greek result or failure are required.");
        if (Context is not null && (Context.Contract.ContractId != Source.ContractId
            || Context.Contract.Dataset != Source.Dataset || Context.Contract.PublisherId != Source.PublisherId
            || Context.Contract.InstrumentId != Source.InstrumentId || Context.Contract.RawSymbol != Source.RawSymbol
            || Context.GenerationId != Source.GenerationId))
            throw new ArgumentException("Trade evidence and pricing context identity disagree.");
        if (Greeks is not null && (Context is null || Underlying is null
            || new[] { Greeks.ImpliedVolatility, Greeks.Delta, Greeks.Gamma, Greeks.Theta,
                Greeks.Vega, Greeks.Rho, Greeks.TheoreticalPrice, Greeks.TimeToExpiry }.Any(x => !double.IsFinite(x))))
            throw new ArgumentException("Successful trade evidence requires finite full Greeks and exact context.");
    }
}

/// <summary>Completion acknowledges durable source plus the first immutable pricing attempt, not a transient enqueue.</summary>
public interface IOptionTradeEvidenceWriter
{
    ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken);
}
