using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>An immutable pending assignment revision, applied only by its declared policy.</summary>
[MessagePackObject]
public sealed record ParameterAssignmentRevision(
    [property: Key(0)] Guid AssignmentId,
    [property: Key(1)] ParameterAssignmentScope Scope,
    [property: Key(2)] long Revision,
    [property: Key(3)] ParameterVersionRef Reference,
    [property: Key(4)] bool Enabled,
    [property: Key(5)] ParameterApplicationPolicy ApplicationPolicy,
    [property: Key(6)] DateTime CreatedAtUtc,
    [property: Key(7)] string CreatedBy);
