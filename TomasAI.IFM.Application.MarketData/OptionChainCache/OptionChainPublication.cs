using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.OptionChainCache;
[MessagePackObject]
public sealed record OptionChainPublication([property: Key(0)] MarketCompositionSnapshot Snapshot,
    [property: Key(1)] DateOnly ValueDate, [property: Key(2)] string ConfigurationDigest,
    [property: Key(3)] long Version, [property: Key(4)] string LookupScope);
