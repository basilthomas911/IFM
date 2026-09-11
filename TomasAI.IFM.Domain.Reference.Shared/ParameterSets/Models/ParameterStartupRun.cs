using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterStartupRun(
 [property:Key(0)] Guid RunId,[property:Key(1)] ParameterAssignmentRevision[] Scopes,
 [property:Key(2)] ParameterSetVersion[] Versions,[property:Key(3)] ParameterSignalStartupPlan Plan,
 [property:Key(4)] DateTime CreatedAtUtc,[property:Key(5)] string CreatedBy);
