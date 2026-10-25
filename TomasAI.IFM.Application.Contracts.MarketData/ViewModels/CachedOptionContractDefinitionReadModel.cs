using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[MessagePackObject]
public sealed record CachedOptionContractDefinitionReadModel
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;
    [Key(1)] public string UnderlyingContractId { get; init; } = string.Empty;
    [Key(2)] public DateOnly ExpiryDate { get; init; }
    [Key(3)] public string ProviderRoot { get; init; } = string.Empty;
    [Key(4)] public string OptionFamily { get; init; } = string.Empty;
    [Key(5)] public FuturesOptionContractReadModel Definition { get; init; } = new();
    [Key(6)] public DateTime RefreshedAtUtc { get; init; }
}
