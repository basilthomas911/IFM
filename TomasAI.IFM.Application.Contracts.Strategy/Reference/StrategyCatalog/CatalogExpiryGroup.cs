using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogExpiryGroup(
    [property: Key(0)] string Key,
    [property: Key(1)] string? AfterGroup = null);
