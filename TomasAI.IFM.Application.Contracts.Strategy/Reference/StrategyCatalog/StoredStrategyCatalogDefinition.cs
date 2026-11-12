using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record StoredStrategyCatalogDefinition(
    [property: Key(0)] StrategyCatalogDefinition Definition,
    [property: Key(1)] string ContentHash,
    [property: Key(2)] CatalogLifecycleStatus Status,
    [property: Key(3)] DateTime CreatedUtc,
    [property: Key(4)] string CreatedBy,
    [property: Key(5)] DateTime? EffectiveFromUtc,
    [property: Key(6)] string? PublishedBy,
    [property: Key(7)] DateTime? RetiredAtUtc,
    [property: Key(8)] string? RetiredBy);
