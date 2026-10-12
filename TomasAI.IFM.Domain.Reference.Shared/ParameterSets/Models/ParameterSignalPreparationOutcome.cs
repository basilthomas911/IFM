using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterSignalPreparationOutcome(
 [property: Key(0)] ParameterSignalProducerKey Key,
 [property: Key(1)] ParameterSignalPreparationStatus Status,
 [property: Key(2)] string Detail);
