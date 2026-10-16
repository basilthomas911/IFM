using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Audit evidence committed with the authoritative lifecycle fact.</summary>
[MessagePackObject]
public sealed record ParameterAuditEntry(
    [property: Key(0)] Guid OperationId,
    [property: Key(1)] Guid EntityId,
    [property: Key(2)] long Revision,
    [property: Key(3)] string Action,
    [property: Key(4)] string ActorIdentity,
    [property: Key(5)] DateTime RecordedAtUtc,
    [property: Key(6)] string BeforeJson,
    [property: Key(7)] string AfterJson)
{
    [Key(8)] public ParameterAuditValue? Before { get; init; }
    [Key(9)] public ParameterAuditValue? After { get; init; }
}

/// <summary>Typed audit evidence for version lifecycle metadata or assignment changes.</summary>
[MessagePackObject]
public sealed record ParameterAuditValue(
    [property:Key(0)] ParameterVersionRef? Reference = null,
    [property:Key(1)] string? Name = null,
    [property:Key(2)] string? Description = null,
    [property:Key(3)] int? SchemaVersion = null,
    [property:Key(4)] ParameterVersionStatus? Status = null,
    [property:Key(5)] ParameterAssignmentRevision? Assignment = null);
