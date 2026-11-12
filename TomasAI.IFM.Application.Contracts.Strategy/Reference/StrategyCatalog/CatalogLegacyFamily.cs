using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogLegacyFamily(
    [property: Key(0)] int Id,
    [property: Key(1)] long Version);
