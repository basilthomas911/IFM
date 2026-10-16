using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;

[MessagePackObject(AllowPrivate=true)]
internal sealed record OptionContractDisplay(
    [property:Key("contractId")] string ContractId,
    [property:Key("strike")] string Strike,
    [property:Key("isCall")] bool IsCall,
    [property:Key("bid")] string? Bid,
    [property:Key("ask")] string? Ask,
    [property:Key("bidSize")] uint? BidSize,
    [property:Key("askSize")] uint? AskSize,
    [property:Key("last")] string? Last,
    [property:Key("lastSize")] uint? LastSize,
    [property:Key("impliedVolatility")] double? ImpliedVolatility,
    [property:Key("theoreticalPrice")] double? TheoreticalPrice,
    [property:Key("delta")] double? Delta,
    [property:Key("gamma")] double? Gamma,
    [property:Key("vega")] double? Vega,
    [property:Key("theta")] double? Theta,
    [property:Key("rho")] double? Rho,
    [property:Key("volume")] long? Volume,
    [property:Key("openInterest")] long? OpenInterest,
    [property:Key("greeksValid")] bool GreeksValid,
    [property:Key("isStale")] bool IsStale,
    [property:Key("quoteAtUtc")] string? QuoteAtUtc,
    [property:Key("tradeAtUtc")] string? TradeAtUtc,
    [property:Key("evaluatedAtUtc")] string? EvaluatedAtUtc,
    [property:Key("volumeValueDate")] string? VolumeValueDate,
    [property:Key("openInterestValueDate")] string? OpenInterestValueDate,
    [property:Key("selectionValid")] bool SelectionValid,
    [property:Key("quoteUnavailableReason")] string? QuoteUnavailableReason,
    [property:Key("lastQuoteEventAtUtc")] string? LastQuoteEventAtUtc,
    [property:Key("lastQuoteReceivedAtUtc")] string? LastQuoteReceivedAtUtc)
{
    public static OptionContractDisplay From(EvaluatedOptionContractReadModel v)=>new(
        v.ContractId,
        v.Strike.ToString(CultureInfo.InvariantCulture),
        v.IsCall,
        v.Bid?.ToString(CultureInfo.InvariantCulture),
        v.Ask?.ToString(CultureInfo.InvariantCulture),
        v.BidSize,
        v.AskSize,
        v.Last?.ToString(CultureInfo.InvariantCulture),
        v.LastSize,
        v.ImpliedVolatility,
        v.TheoreticalPrice,
        v.Delta,
        v.Gamma,
        v.Vega,
        v.Theta,
        v.Rho,
        v.Volume,
        v.OpenInterest,
        v.GreeksValid,
        v.IsStale,
        v.QuoteAtUtc?.ToString("O",CultureInfo.InvariantCulture),
        v.TradeAtUtc?.ToString("O",CultureInfo.InvariantCulture),
        v.EvaluatedAtUtc?.ToString("O",CultureInfo.InvariantCulture),
        v.VolumeValueDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
        v.OpenInterestValueDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
        v.SelectionValid,
        v.QuoteUnavailableReason,
        v.LastQuoteEventAtUtc?.ToString("O",CultureInfo.InvariantCulture),
        v.LastQuoteReceivedAtUtc?.ToString("O",CultureInfo.InvariantCulture));
}

[MessagePackObject(AllowPrivate=true)]
internal sealed record OptionChainDisplay(
    [property:Key("underlyingContractId")] string UnderlyingContractId,
    [property:Key("expiryDate")] string ExpiryDate,
    [property:Key("underlyingPrice")] string? UnderlyingPrice,
    [property:Key("lowerStrikeBound")] string? LowerStrikeBound,
    [property:Key("upperStrikeBound")] string? UpperStrikeBound,
    [property:Key("windowMethod")] string WindowMethod,
    [property:Key("asOfUtc")] string AsOfUtc,
    [property:Key("contracts")] OptionContractDisplay[] Contracts,
    [property:Key("bollingerStandardDeviation20")] string? BollingerStandardDeviation20,
    [property:Key("previousSessionClose")] string? PreviousSessionClose,
    [property:Key("ivRank")] string? IvRank,
    [property:Key("ivRankStatus")] string IvRankStatus,
    [property:Key("bollingerAsOfUtc")] string? BollingerAsOfUtc,
    [property:Key("lastUnderlyingPrice")] string? LastUnderlyingPrice,
    [property:Key("lastUnderlyingTradeAtUtc")] string? LastUnderlyingTradeAtUtc)
{
    public static OptionChainDisplay From(EvaluatedOptionChainReadModel v)=>new(
        v.UnderlyingContractId,
        v.ExpiryDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
        v.UnderlyingPrice?.ToString(CultureInfo.InvariantCulture),
        v.LowerStrikeBound?.ToString(CultureInfo.InvariantCulture),
        v.UpperStrikeBound?.ToString(CultureInfo.InvariantCulture),
        v.WindowMethod,
        v.AsOfUtc.ToString("O",CultureInfo.InvariantCulture),
        Array.ConvertAll(v.Contracts,OptionContractDisplay.From),
        v.BollingerStandardDeviation20?.ToString(CultureInfo.InvariantCulture),
        v.PreviousSessionClose?.ToString(CultureInfo.InvariantCulture),
        v.IvRank?.ToString(CultureInfo.InvariantCulture),
        v.IvRankStatus,
        v.BollingerAsOfUtc?.ToString("O",CultureInfo.InvariantCulture),
        v.LastUnderlyingPrice?.ToString(CultureInfo.InvariantCulture),
        v.LastUnderlyingTradeAtUtc?.ToString("O",CultureInfo.InvariantCulture));
}

[MessagePackObject(AllowPrivate=true)]
internal sealed record SelectedLegDisplay(
    [property:Key("contractId")] string ContractId,
    [property:Key("role")] string Role,
    [property:Key("strike")] string? Strike,
    [property:Key("bid")] string? Bid,
    [property:Key("ask")] string? Ask,
    [property:Key("delta")] double? Delta,
    [property:Key("isCall")] bool IsCall,
    [property:Key("vega")] double? Vega,
    [property:Key("multiplier")] string? Multiplier,
    [property:Key("quoteAtUtc")] string? QuoteAtUtc,
    [property:Key("frozen")] bool Frozen,
    [property:Key("isFuture")] bool IsFuture,
    [property:Key("sign")] int Sign)
{
    public static SelectedLegDisplay From(BrokerTradeLegData v)=>new(
        v.ContractId,
        v.Role,
        v.Strike?.ToString(CultureInfo.InvariantCulture),
        v.Bid?.ToString(CultureInfo.InvariantCulture),
        v.Ask?.ToString(CultureInfo.InvariantCulture),
        v.Delta,
        v.IsCall,
        v.Vega,
        v.Multiplier?.ToString(CultureInfo.InvariantCulture),
        v.QuoteAtUtc?.ToString("O",CultureInfo.InvariantCulture),
        v.Frozen,
        v.IsFuture,
        v.Sign);
}
