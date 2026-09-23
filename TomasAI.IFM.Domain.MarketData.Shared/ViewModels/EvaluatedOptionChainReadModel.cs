using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[MessagePackObject]
public sealed record EvaluatedOptionChainReadModel(
    [property: Key(0)] string UnderlyingContractId,
    [property: Key(1)] DateOnly ExpiryDate,
    [property: Key(2)] decimal? UnderlyingPrice,
    [property: Key(3)] decimal? LowerStrikeBound,
    [property: Key(4)] decimal? UpperStrikeBound,
    [property: Key(5)] string WindowMethod,
    [property: Key(6)] DateTimeOffset AsOfUtc,
    [property: Key(7)] EvaluatedOptionContractReadModel[] Contracts);

[MessagePackObject]
public sealed record EvaluatedOptionContractReadModel(
    [property: Key(0)] string ContractId,
    [property: Key(1)] decimal Strike,
    [property: Key(2)] bool IsCall,
    [property: Key(3)] decimal? Bid,
    [property: Key(4)] decimal? Ask,
    [property: Key(5)] uint? BidSize,
    [property: Key(6)] uint? AskSize,
    [property: Key(7)] decimal? Last,
    [property: Key(8)] uint? LastSize,
    [property: Key(9)] double? ImpliedVolatility,
    [property: Key(10)] double? TheoreticalPrice,
    [property: Key(11)] double? Delta,
    [property: Key(12)] double? Gamma,
    [property: Key(13)] double? Vega,
    [property: Key(14)] double? Theta,
    [property: Key(15)] double? Rho,
    [property: Key(16)] long? Volume,
    [property: Key(17)] long? OpenInterest,
    [property: Key(18)] bool GreeksValid,
    [property: Key(19)] bool IsStale,
    [property: Key(20)] DateTimeOffset? QuoteAtUtc,
    [property: Key(21)] DateTimeOffset? TradeAtUtc,
    [property: Key(22)] DateTimeOffset? EvaluatedAtUtc);
