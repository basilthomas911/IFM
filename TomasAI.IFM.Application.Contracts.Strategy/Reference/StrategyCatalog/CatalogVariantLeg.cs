using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogVariantLeg(
    [property: Key(0)] string LegKey,
    [property: Key(1)] string Side,
    [property: Key(2)] decimal Ratio);
