using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterSignalDemand([property: Key(0)] ParameterSignalProducerKey Key, [property: Key(1)] string Consumer, [property: Key(2)] Guid RequirementId, [property: Key(3)] bool Prepare, [property: Key(4)] bool Monitor, [property: Key(5)] int MaximumAgeSeconds);

