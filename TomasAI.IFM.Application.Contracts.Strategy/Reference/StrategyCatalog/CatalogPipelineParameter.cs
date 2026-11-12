using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogPipelineParameter(
    [property: Key(0)] string Role,
    [property: Key(1)] CatalogPipelineParameterKind Kind,
    [property: Key(2)] Guid Id,
    [property: Key(3)] int Version,
    [property: Key(4)] string Hash);
