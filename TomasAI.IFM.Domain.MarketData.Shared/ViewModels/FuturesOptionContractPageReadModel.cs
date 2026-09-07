using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[MessagePackObject]
public sealed record FuturesOptionContractPageReadModel(
    [property: Key(0)] FuturesOptionContractReadModel[] Items,
    [property: Key(1)] string? ContinuationToken)
{
    [IgnoreMember] public bool HasMore => ContinuationToken is not null;
}
