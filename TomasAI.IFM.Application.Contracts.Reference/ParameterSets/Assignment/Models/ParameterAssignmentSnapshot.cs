using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterAssignmentSnapshot(
 [property: Key(0)] ParameterAssignmentEntityId EntityId,
 [property: Key(1)] ParameterAssignmentScope Scope,
 [property: Key(2)] long Revision,
 [property: Key(3)] ParameterAssignmentRevision? Assignment,
 [property: Key(4)] ParameterAuditEntry[]? Audit = null);
