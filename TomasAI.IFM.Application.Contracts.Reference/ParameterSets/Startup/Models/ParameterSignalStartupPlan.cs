using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterSignalStartupPlan([property: Key(0)] Guid StartupRunId, [property: Key(1)] string AssignmentFingerprint, [property: Key(2)] string Fingerprint, [property: Key(3)] ParameterSignalStartupStep[] Steps, [property: Key(4)] string[]? Issues = null);

