using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Generic consumer identity, role and canonical domain-owned scope.</summary>
[MessagePackObject]
public sealed record ParameterAssignmentScope(
    [property: Key(0)] string ConsumerKindCode,
    [property: Key(1)] string ConsumerId,
    [property: Key(2)] string Role,
    [property: Key(3)] string ComponentCode,
    [property: Key(4)] string ScopeJson,
    [property: Key(5)] string ScopeSha256);

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

[MessagePackObject]
public sealed record AppliedParameterAssignment(
    [property: Key(0)] Guid StartupRunId,
    [property: Key(1)] ParameterAssignmentRevision Assignment,
    [property: Key(2)] ParameterSetVersion Version);

[MessagePackObject]
public sealed record ParameterAssignmentSnapshot(
 [property:Key(0)] ParameterAssignmentEntityId EntityId,
 [property:Key(1)] ParameterAssignmentScope Scope,
 [property:Key(2)] long Revision,
 [property:Key(3)] ParameterAssignmentRevision? Assignment,
 [property:Key(4)] ParameterAuditEntry[]? Audit = null);
