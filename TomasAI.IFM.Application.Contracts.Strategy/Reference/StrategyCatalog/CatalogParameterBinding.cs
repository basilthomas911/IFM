using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogParameterBinding(
    [property: Key(0)] string Role,
    [property: Key(1)] CatalogKey ParameterSet);
