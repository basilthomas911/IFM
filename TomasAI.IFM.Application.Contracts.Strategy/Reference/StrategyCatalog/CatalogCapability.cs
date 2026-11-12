using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogCapability(
    [property: Key(0)] string Role,
    [property: Key(1)] string Code,
    [property: Key(2)] int Version);
