using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterSignalProducerKey([property: Key(0)] ParameterSignalProducer Producer, [property: Key(1)] TimeFrameType Interval, [property: Key(2)] int Period = 0);

