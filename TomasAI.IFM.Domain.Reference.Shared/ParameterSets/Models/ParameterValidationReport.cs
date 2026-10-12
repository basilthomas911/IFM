using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterValidationReport(
    [property: Key(0)] string CandidateSha256,
    [property: Key(1)] ParameterValidationIssue[] Issues)
{
    [IgnoreMember] public bool IsValid => Issues.All(x => x.Severity != ParameterIssueSeverity.Error);
}
