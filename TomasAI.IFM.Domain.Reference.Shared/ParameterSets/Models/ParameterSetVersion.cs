using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterSetVersion(
    [property: Key(0)] ParameterVersionRef Reference,
    [property: Key(1)] string Name, [property: Key(2)] string Description,
    [property: Key(3)] int SchemaVersion, [property: Key(4)] ParameterVersionStatus Status,
    [property: Key(5)] string PayloadJson, [property: Key(6)] DateTime CreatedAtUtc,
    [property: Key(7)] string CreatedBy, [property: Key(8)] DateTime? PublishedAtUtc = null,
    [property: Key(9)] DateTime? RetiredAtUtc = null, [property: Key(10)] long CatalogRevision = 0,
    [property: Key(11)] ParameterLegacyReference? LegacySource = null);
