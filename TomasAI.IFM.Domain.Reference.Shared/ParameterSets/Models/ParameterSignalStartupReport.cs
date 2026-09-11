using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
// Preparation evidence never implies that a signal is warm or fresh.
public enum ParameterSignalPreparationStatus { NotRequested=0, ExistingRoute=1, Accepted=2, Failed=3, TimedOut=4, MarketClosed=5, Cancelled=6 }
[MessagePackObject]
public sealed record ParameterSignalPreparationOutcome(
 [property:Key(0)] ParameterSignalProducerKey Key,
 [property:Key(1)] ParameterSignalPreparationStatus Status,
 [property:Key(2)] string Detail);
[MessagePackObject]
public sealed record ParameterSignalStartupReport(
 [property:Key(0)] Guid RunId,
 [property:Key(1)] string PlanFingerprint,
 [property:Key(2)] DateOnly ValueDate,
 [property:Key(3)] string ContractId,
 [property:Key(4)] DateTime RecordedAtUtc,
 [property:Key(5)] ParameterSignalPreparationOutcome[] Outcomes);
