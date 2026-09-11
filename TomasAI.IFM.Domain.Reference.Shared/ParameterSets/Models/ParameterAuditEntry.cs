using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Audit evidence committed with the authoritative lifecycle fact.</summary>
[MessagePackObject]
public sealed record ParameterAuditEntry(
    [property:Key(0)] Guid OperationId,
    [property:Key(1)] Guid EntityId,
    [property:Key(2)] long Revision,
    [property:Key(3)] string Action,
    [property:Key(4)] string ActorIdentity,
    [property:Key(5)] DateTime RecordedAtUtc,
    [property:Key(6)] string BeforeJson,
    [property:Key(7)] string AfterJson);
