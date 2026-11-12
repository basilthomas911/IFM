using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed record CatalogLeg(
    [property: Key(0)] string Key,
    [property: Key(1)] string InstrumentClass,
    [property: Key(2)] string Side,
    [property: Key(3)] string OptionRight,
    [property: Key(4)] decimal Ratio,
    [property: Key(5)] string ExpiryGroup);
