using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "Instance validation reports the configuration property captured by this object.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "Instance validation reports the configuration property captured by this object.")]
    public void Validate()
    {
        if (Source is null || string.IsNullOrWhiteSpace(Source.Dataset) || Source.Dataset.Length > 64
            || string.IsNullOrWhiteSpace(Source.ContractId) || Source.ContractId.Length > 128
            || Source.PublisherId == 0 || Source.InstrumentId == 0 || Source.ValueDate == default
            || string.IsNullOrWhiteSpace(Source.RawSymbol) || Source.RawSymbol.Length > 256
            || Source.GenerationId == Guid.Empty || CalculatedAtUtc.Offset != TimeSpan.Zero
            || !string.Equals(PriceBasis,"Trade",StringComparison.Ordinal) || (Greeks is null) == (Failure is null))
            throw new ArgumentException("An identified source and exactly one full-Greek result or failure are required.");
        if (Context is not null && (!string.Equals(Context.Contract.ContractId,Source.ContractId,StringComparison.Ordinal)
            || !string.Equals(Context.Contract.Dataset,Source.Dataset,StringComparison.Ordinal) || Context.Contract.PublisherId != Source.PublisherId
            || Context.Contract.InstrumentId != Source.InstrumentId || !string.Equals(Context.Contract.RawSymbol,Source.RawSymbol,StringComparison.Ordinal)
            || Context.GenerationId != Source.GenerationId))
            throw new ArgumentException("Trade evidence and pricing context identity disagree.");
        if (Greeks is not null && (Context is null || Underlying is null
            || new[] { Greeks.ImpliedVolatility, Greeks.Delta, Greeks.Gamma, Greeks.Theta,
                Greeks.Vega, Greeks.Rho, Greeks.TheoreticalPrice, Greeks.TimeToExpiry }.Any(x => !double.IsFinite(x))))
            throw new ArgumentException("Successful trade evidence requires finite full Greeks and exact context.");
    }
}
