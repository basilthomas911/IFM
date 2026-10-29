using System.Runtime.InteropServices;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Pricing;
using TomasAI.IFM.Shared.OptionPricing;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Maps native cached calculations to the existing application types.
/// Reads acquire no subscription and run no numerical solver. Application
/// reference validation and semantic identity preparation remain application policy.
/// A caller must supply the exact frozen context and authoritative API time.</summary>
public sealed class NativeOptionPricingCache(NativeMarketDataRuntime runtime)
{
    static bool PolicyMatches(NativeScopeCalculation frame,string version){
        if(string.IsNullOrWhiteSpace(version)||version.Length>1024)return false;
        Span<byte> text=stackalloc byte[4096];var count=System.Text.Encoding.UTF8.GetBytes(version,text);if(count>1024)return false;
        Span<byte> bytes=stackalloc byte[32];System.Security.Cryptography.SHA256.HashData(text[..count],bytes);
        return frame.Policy0==System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes)
            &&frame.Policy1==System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..])
            &&frame.Policy2==System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..])
            &&frame.Policy3==System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]);
    }
    static DateTimeOffset At(long ticks) => DateTimeOffset.UnixEpoch.AddTicks(ticks);
    static Guid Identity(ulong low, ulong high)
    {
        Span<byte> bytes = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes, low);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], high); return new(bytes);
    }
    static decimal Decimal(NativeDecimal value)
    {
        var scale = (value.Flags >> 16) & 255;
        if ((value.Flags & 0x7f00ffff) != 0 || scale > 28) throw new InvalidDataException("Malformed cached quote decimal.");
        return new(unchecked((int)value.Low), unchecked((int)value.Middle), unchecked((int)value.High), (value.Flags >> 31) != 0, (byte)scale);
    }
    internal static OptionPricingQuote Quote(NativePricingQuote value, string contract) => new(contract,
        Decimal(value.Bid), Decimal(value.Ask), Decimal(value.BidSize), Decimal(value.AskSize),
        At(value.Event).ToOffset(TimeSpan.FromMinutes(value.EventOffset)), At(value.Received).ToOffset(TimeSpan.FromMinutes(value.ReceivedOffset)),
        value.Sequence, Identity(value.GenerationLow, value.GenerationHigh));
    static OptionPricingFailure Pending(string contract, string code) => new(code, "OptionPricing", contract, "Qualified calculation is unavailable.");
    static string Numerical(uint code) => code switch
    {
        1 => "InvalidInput", 2 => "Expired", 3 => "UndefinedGreeks", 4 => "PriceOutOfBounds", 5 => "ImpliedVolatilityNotIdentifiable",
        6 => "VolatilityNotBracketed", 7 => "NonConvergence", 8 => "NumericalFailure",
        _ => throw new InvalidDataException("Lifecycle/allocation failure cannot be presented as cached financial risk.")
    };
    static OptionPricingFailure? Failure(NativeScopeCalculation value, PreparedOptionPricingContext prepared, bool risk)
    {
        var context = prepared.Context; var id = context.Contract.ContractId;
        if ((value.Flags & 16) == 0) return null;
        if (value.FailureKind == 4)
            return risk ? new("GreeksCalculationFailed", Numerical(value.FailureCode), id, "Option pricing inputs do not satisfy the qualified context.")
                : Pending(id, Numerical(value.FailureCode));
        if (value.FailureKind == 3)
        {
            var source = value.FailureInput == 0 ? context.Contract.UnderlyingContractId : id;
            var (code, field) = value.FailureCode switch
            {
                1 => ("Recovering", "Generation"), 2 => ("ContractMetadataUnavailable", "Identity/Strike"),
                3 => ("InvalidQuote", source), 4 => ("StaleData", source), 5 => ("IncoherentQuotes", "SourceTimeSkew"),
                _ => throw new InvalidDataException("Malformed native cached quote failure.")
            };
            return new(code, field, id, "Option pricing inputs do not satisfy the qualified context.");
        }
        return Pending(id, value.FailureKind switch { 1 => "UnderlyingQuoteUnavailable", 2 or 5 => "StaleData", 6 => "CalculationPending",
            _ => throw new InvalidDataException("Malformed native cached failure.") });
    }
    NativeScopeCalculation? Read(string scope, Guid generation, PreparedOptionPricingContext prepared, bool risk)
    {
        ArgumentNullException.ThrowIfNull(prepared); ArgumentNullException.ThrowIfNull(prepared.Context);
        NativeScopeCalculation frame;
        try { frame = runtime.ReadScopeCalculation(scope, generation, prepared.Context.Contract.InstrumentId, risk); }
        catch (ObjectDisposedException) { return null; } // A retired API epoch is unavailable.
        if (frame.Flags == 0 || Identity(frame.ContextLow, frame.ContextHigh) != prepared.Identity) return null;
        if (Identity(frame.PhysicalLow, frame.PhysicalHigh) == Guid.Empty || !double.IsFinite(frame.Time))
            throw new InvalidDataException("Malformed native cached context/source identity.");
        return frame;
    }
    /// <summary>Reads the existing coalesced selection/IV result. Superseded
    /// contexts return no value; stale source evidence retains provenance and
    /// returns the same unavailable price/delta and failure as the current path.</summary>
    public unsafe ChainSelectionCalculation? ReadSelection(string scope, Guid generation, PreparedOptionPricingContext prepared,
        string policyVersion, DateTimeOffset currentAt)
    {
        var cached = Read(scope, generation, prepared, false); if (cached is not { } frame) return null;
        if(!PolicyMatches(frame,policyVersion))return null;
        var context = prepared.Context; var underlying = Quote(frame.Underlying, context.Contract.UnderlyingContractId);
        var option = Quote(frame.Option, context.Contract.ContractId); ChainIvObservation? iv = null;
        if ((frame.Flags & 8) != 0)
        {
            if (Identity(frame.IvContextLow, frame.IvContextHigh) != prepared.Identity || !double.IsFinite(frame.IvVolatility))
                throw new InvalidDataException("Malformed retained native IV context.");
            iv = new(frame.IvVolatility, context, Quote(frame.IvUnderlying, context.Contract.UnderlyingContractId),
                Quote(frame.IvOption, context.Contract.ContractId), At(frame.IvAt), policyVersion);
        }
        var failure = Failure(frame, prepared, false);
        if (Black76PricingModel.ValidateInputs(context, underlying, option, prepared.Strike, prepared.IsCall, currentAt) is not null)
            failure = Pending(context.Contract.ContractId, "StaleData");
        if ((frame.Flags & 2) != 0 && (!double.IsFinite(frame.Values[0]) || !double.IsFinite(frame.Values[2])))
            throw new InvalidDataException("Nonfinite native selection result.");
        return new(context, underlying, option, iv, failure is null && (frame.Flags & 2) != 0 ? frame.Values[0] : null,
            failure is null && (frame.Flags & 2) != 0 ? frame.Values[2] : null, At(frame.At), failure);
    }
    /// <summary>Reads full risk calculated in Rust and prepares the unchanged
    /// application semantic digest from the exact captured inputs and API time.
    /// This performs validation/hashing only; no pricing or IV work occurs here.</summary>
    public unsafe OptionPricingPassResult? ReadRisk(string scope, Guid generation, PreparedOptionPricingContext prepared, DateTimeOffset currentAt)
    {
        var cached = Read(scope, generation, prepared, true); if (cached is not { } frame) return null;
        return Risk(frame, prepared, currentAt);
    }
    static unsafe OptionPricingPassResult Risk(NativeScopeCalculation frame, PreparedOptionPricingContext prepared, DateTimeOffset currentAt)
    {
        var context = prepared.Context; var underlying = Quote(frame.Underlying, context.Contract.UnderlyingContractId);
        var option = Quote(frame.Option, context.Contract.ContractId);
        var failure = Failure(frame, prepared, true);
        // Match ReadRisk's precedence: superseded source first, then context/age.
        if (frame.FailureKind != 6 && Black76PricingModel.ValidateInputs(context, underlying, option, prepared.Strike, prepared.IsCall, currentAt) is not null)
            failure = Pending(context.Contract.ContractId, "StaleData");
        if (failure is not null) return new(null, failure);
        if ((frame.Flags & 4) == 0) throw new InvalidDataException("Native risk cache has neither risk nor an explicit failure.");
        for (var i = 0; i < 7; ++i) if (!double.IsFinite(frame.Values[i])) throw new InvalidDataException("Nonfinite native risk result.");
        var strike = prepared.Strike; var isCall = prepared.IsCall; var at = At(frame.At); var t = frame.Time;
        var digest = PricingSemanticHash.Compute(new { Version = 2, context, underlying, option, strike, isCall, at, t,
            EngineVersion = context.PricerVersion, NumericalPolicy = new PricingSettings(), PolicyVersion = PricingSettings.Version });
        return new(new(frame.Values[1], frame.Values[2], frame.Values[3], frame.Values[4], frame.Values[5], frame.Values[6], frame.Values[0], t, digest), null);
    }
    /// <summary>Returns quote-derived Greeks and their exact source observation
    /// from one frozen native calculation. Consumers must match both the physical
    /// identity and source key before attaching this result to a raw quote. This
    /// performs no solver work and never reads a later underlying hot value.</summary>
    public (OptionPricingQuote Quote, OptionGreeksSnapshot Greeks, Guid PhysicalGeneration)? ReadQuoteGreeks(
        string scope, Guid generation, PreparedOptionPricingContext prepared, DateTimeOffset currentAt)
    {
        var cached = Read(scope, generation, prepared, true); if (cached is not { } frame) return null;
        var context = prepared.Context;
        var result = Risk(frame, prepared, currentAt);
        var option = Quote(frame.Option, context.Contract.ContractId);
        if (result.Failure is { Code: not "GreeksCalculationFailed" })
            return (option, new() { IsValid = false, FuturesContractId = context.Contract.UnderlyingContractId,
                CalculatedAtUtc = At(frame.At), FailureReason = OptionGreeksFailureReason.PricingContextUnavailable,
                PricingFailure = result.Failure }, Identity(frame.PhysicalLow, frame.PhysicalHigh));
        var underlying = Quote(frame.Underlying, context.Contract.UnderlyingContractId);
        var g = result.Value;
        var greeks = new OptionGreeksSnapshot
        {
            IsValid = g is not null, PriceSource = OptionGreeksPriceSource.QuoteMidpoint,
            FuturesContractId = context.Contract.UnderlyingContractId,
            FuturesPrice = underlying.Bid / 2m + underlying.Ask / 2m,
            OptionMarkPrice = option.Bid / 2m + option.Ask / 2m,
            RiskFreeRate = context.Rate.AnnualContinuousRate, TimeToExpiryYears = g?.TimeToExpiry,
            ImpliedVolatility = g?.ImpliedVolatility, TheoreticalPrice = g?.TheoreticalPrice,
            Delta = g?.Delta, Gamma = g?.Gamma, Theta = g?.Theta, Vega = g?.Vega, Rho = g?.Rho,
            FuturesPriceSourceSequence = underlying.Sequence, OptionPriceSourceSequence = option.Sequence,
            FuturesPriceTimestamp = underlying.EventAtUtc, OptionPriceTimestamp = option.EventAtUtc,
            CalculatedAtUtc = At(frame.At), PricingContextDigest = g?.ContextDigest, PricingFailure = result.Failure
        };
        return (option, greeks, Identity(frame.PhysicalLow, frame.PhysicalHigh));
    }
}
