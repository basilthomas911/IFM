using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
[MessagePackObject]
public sealed record FuturesOptionContractPageReadModel(
    [property: Key(0)] FuturesOptionContractReadModel[] Items,
    [property: Key(1)] string? ContinuationToken)
{
    [IgnoreMember] public bool HasMore => ContinuationToken is not null;
}
