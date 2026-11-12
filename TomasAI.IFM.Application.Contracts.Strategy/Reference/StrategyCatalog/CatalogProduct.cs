using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogProduct(
    [property: Key(0)] int ProductId,
    [property: Key(1)] string Symbol,
    [property: Key(2)] string Exchange,
    [property: Key(3)] string Currency);
