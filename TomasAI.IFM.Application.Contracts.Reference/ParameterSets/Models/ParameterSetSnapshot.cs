using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Projected parameter set detail, including version, operation, and audit history.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterSetSnapshot(
    [property: Key(0)] Guid SetId,
    [property: Key(1)] long Revision,
    [property: Key(2)] ParameterSetVersion[] Versions,
    [property: Key(3)] ParameterOperationReceipt[] Operations,
    [property: Key(4)] ParameterAuditEntry[]? Audit = null);
