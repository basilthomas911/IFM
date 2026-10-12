using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
[MessagePackObject]
public sealed record EvaluatedOptionChainReadModel(
    [property: Key(0)] string UnderlyingContractId,
    [property: Key(1)] DateOnly ExpiryDate,
    [property: Key(2)] decimal? UnderlyingPrice,
    [property: Key(3)] decimal? LowerStrikeBound,
    [property: Key(4)] decimal? UpperStrikeBound,
    [property: Key(5)] string WindowMethod,
    [property: Key(6)] DateTimeOffset AsOfUtc,
    [property: Key(7)] EvaluatedOptionContractReadModel[] Contracts)
{
    [Key(8)] public decimal? BollingerStandardDeviation20 { get; init; }
    [Key(9)] public decimal? PreviousSessionClose { get; init; }
    [Key(10)] public decimal? IvRank { get; init; }
    [Key(11)] public string IvRankStatus { get; init; } = "Waiting for IV observation";
    [Key(12)] public DateTimeOffset? BollingerAsOfUtc { get; init; }
}
