using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject]
public sealed record AppliedParameterAssignment(
    [property: Key(0)] Guid StartupRunId,
    [property: Key(1)] ParameterAssignmentRevision Assignment,
    [property: Key(2)] ParameterSetVersion Version);
