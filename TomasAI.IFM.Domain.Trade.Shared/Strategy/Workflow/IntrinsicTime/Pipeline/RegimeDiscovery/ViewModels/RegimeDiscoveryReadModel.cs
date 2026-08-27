using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;

/// <summary>Represents the rebuildable terminal Regime Discovery projection for one workflow execution.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryReadModel
{
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(0)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the stable workflow entity routing identity.</summary>
    [Key(1)] public string WorkflowEntityId { get; init; } = string.Empty;
    /// <summary>Gets the immutable workflow input revision.</summary>
    [Key(2)] public long InputWorkflowRevision { get; init; }
    /// <summary>Gets the Start command identity.</summary>
    [Key(3)] public Guid CommandId { get; init; }
    /// <summary>Gets the terminal private event identity.</summary>
    [Key(4)] public Guid SourceEventId { get; init; }
    /// <summary>Gets the terminal private event-log sequence.</summary>
    [Key(5)] public long SourceEventSequence { get; init; }
    /// <summary>Gets the terminal status text.</summary>
    [Key(6)] public string Status { get; init; } = string.Empty;
    /// <summary>Gets the frozen parameter payload hash.</summary>
    [Key(7)] public string ParameterPayloadSha256 { get; init; } = string.Empty;
    /// <summary>Gets the immutable market-signal snapshot identity.</summary>
    [Key(8)] public Guid SignalSnapshotId { get; init; }
    /// <summary>Gets the complete typed result payload, or an empty buffer on failure.</summary>
    [Key(9)] public ReadOnlyMemory<byte> ResultPayload { get; init; }
    /// <summary>Gets the deterministic result payload hash.</summary>
    [Key(10)] public string ResultPayloadSha256 { get; init; } = string.Empty;
    /// <summary>Gets the stable failure code, or zero on success.</summary>
    [Key(11)] public int FailureCode { get; init; }
    /// <summary>Gets the safe failure message.</summary>
    [Key(12)] public string FailureMessage { get; init; } = string.Empty;
    /// <summary>Gets the serialized structured reason payload.</summary>
    [Key(13)] public ReadOnlyMemory<byte> ReasonsPayload { get; init; }
    /// <summary>Gets the read-model schema version.</summary>
    [Key(14)] public int SchemaVersion { get; init; }
    /// <summary>Gets the UTC terminal timestamp.</summary>
    [Key(15)] public DateTime TerminalAtUtc { get; init; }
    /// <summary>Gets the UTC projection timestamp.</summary>
    [Key(16)] public DateTime UpdatedAtUtc { get; init; }
}
