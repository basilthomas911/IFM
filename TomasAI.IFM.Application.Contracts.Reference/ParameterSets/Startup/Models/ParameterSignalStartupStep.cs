using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterSignalStartupStep([property: Key(0)] ParameterSignalProducerKey Key, [property: Key(1)] bool Prepare, [property: Key(2)] bool Monitor, [property: Key(3)] ParameterSignalDemand[] Consumers);

