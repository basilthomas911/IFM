using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterSignalStartupReport(
 [property: Key(0)] Guid RunId,
 [property: Key(1)] string PlanFingerprint,
 [property: Key(2)] DateOnly ValueDate,
 [property: Key(3)] string ContractId,
 [property: Key(4)] DateTime RecordedAtUtc,
 [property: Key(5)] ParameterSignalPreparationOutcome[] Outcomes);
