using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterValidationIssue(
    [property: Key(0)] string Code, [property: Key(1)] string Path,
    [property: Key(2)] string Message,
    [property: Key(3)] ParameterIssueSeverity Severity = ParameterIssueSeverity.Error);
