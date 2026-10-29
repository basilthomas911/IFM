using System.Buffers.Binary;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    static (ulong Low, ulong High) GuidBits(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16]; value.TryWriteBytes(bytes);
        return (BinaryPrimitives.ReadUInt64LittleEndian(bytes), BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]));
    }
    static NativePricingQuote QuoteInput(OptionPricingQuote value)
    {
        var generation = GuidBits(value.GenerationId);
        return new()
        {
            GenerationLow = generation.Low, GenerationHigh = generation.High,
            Event = value.EventAtUtc.UtcTicks - DateTimeOffset.UnixEpoch.Ticks,
            Received = value.ReceivedAtUtc.UtcTicks - DateTimeOffset.UnixEpoch.Ticks,
            Sequence = value.Sequence, Bid = NativeDecimal.From(value.Bid), Ask = NativeDecimal.From(value.Ask),
            BidSize = NativeDecimal.From(value.BidSize), AskSize = NativeDecimal.From(value.AskSize),
            EventOffset = (int)value.EventAtUtc.Offset.TotalMinutes, ReceivedOffset = (int)value.ReceivedAtUtc.Offset.TotalMinutes
        };
    }
    /// <summary>Preserves application reference/calendar qualification and moves
    /// quote freshness, skew, generation, decimal price/quantity and strike rules
    /// to Rust. The hot native call is synchronous, fixed-size and allocation-free.
    /// No provider subscription or numerical calculation is performed.</summary>
    public OptionPricingFailure? ValidateOptionInputs(OptionPricingContext context, OptionPricingQuote underlying,
        OptionPricingQuote option, decimal strike, bool isCall, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(underlying); ArgumentNullException.ThrowIfNull(option);
        var invalid = Black76PricingModel.ValidateContext(context, at);
        if (invalid is not null) return invalid;
        var contract = context.Contract;
        var generation = GuidBits(context.GenerationId);
        var input = new NativeQuoteQualificationRequest
        {
            Size = 304,
            Flags = (string.Equals(underlying.ContractId, contract.UnderlyingContractId, StringComparison.Ordinal) ? 1u : 0)
                | (string.Equals(option.ContractId, contract.ContractId, StringComparison.Ordinal) ? 2u : 0)
                | (contract.SchemaVersion == 3 ? 4u : 0) | (isCall ? 8u : 0) | (contract.Right == PricingOptionRight.Call ? 16u : 0),
            GenerationLow = generation.Low, GenerationHigh = generation.High,
            At = at.UtcTicks - DateTimeOffset.UnixEpoch.Ticks,
            MaximumAge = checked((uint)context.MaximumQuoteAgeMilliseconds), MaximumSkew = checked((uint)context.MaximumQuoteSkewMilliseconds),
            MaximumLead = checked((uint)context.MaximumSourceClockLeadMilliseconds),
            Strike = NativeDecimal.From(strike), DefinitionStrike = NativeDecimal.From(contract.Strike ?? 0),
            Underlying = QuoteInput(underlying), Option = QuoteInput(option)
        };
        NativeQuoteQualificationResult result;
        lock (gate)
        {
            EnsureLiveAccess();
            NativeMarketDataLibrary.Check(library.QualifyQuotes(in input, out result), "qualify option quotes");
        }
        if (result.Size != 16 || result.Reserved != 0 || result.Failure > 5 || result.Input > 1)
            throw new InvalidDataException("Malformed native quote qualification result.");
        if (result.Failure == 0) return null;
        var (code, field) = result.Failure switch
        {
            1 => ("Recovering", "Generation"),
            2 => ("ContractMetadataUnavailable", "Identity/Strike"),
            3 => ("InvalidQuote", result.Input == 0 ? underlying.ContractId : option.ContractId),
            4 => ("StaleData", result.Input == 0 ? underlying.ContractId : option.ContractId),
            5 => ("IncoherentQuotes", "SourceTimeSkew"),
            _ => throw new InvalidDataException("Unknown quote qualification result.")
        };
        return new(code, field, contract.ContractId, "Option pricing inputs do not satisfy the qualified context.");
    }
}
