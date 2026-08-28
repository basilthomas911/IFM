using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Represents the immutable public workflow state for one strategy pipeline stage.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyWorkflowStageState
{
    [IgnoreMember]
    string[] _continuationReasonCodes = [];

    /// <summary>Gets the stage processing status.</summary>
    [Key(0)]
    public StrategyActorProcessingStatus ProcessingStatus { get; init; }

    /// <summary>Gets the workflow continuation decision made after stage processing.</summary>
    [Key(1)]
    public StrategyWorkflowContinuationDecision ContinuationDecision { get; init; }

    /// <summary>Gets the UTC timestamp at which stage processing started.</summary>
    [Key(2)]
    public DateTime? StartedAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp at which stage processing completed successfully.</summary>
    [Key(3)]
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp at which stage processing failed.</summary>
    [Key(4)]
    public DateTime? FailedAtUtc { get; init; }

    /// <summary>Gets the accepted opaque result produced by the stage.</summary>
    [Key(5)]
    public StrategyStageResultEnvelope? Result { get; init; }

    /// <summary>Gets the stable continuation rule-set identifier used for the decision.</summary>
    [Key(6)]
    public string ContinuationRuleSetId { get; init; } = string.Empty;

    /// <summary>Gets the continuation rule-set version used for the decision.</summary>
    [Key(7)]
    public int ContinuationRuleSetVersion { get; init; }

    /// <summary>Gets a defensive copy of the reason codes supporting the continuation decision.</summary>
    [Key(8)]
    public string[] ContinuationReasonCodes
    {
        get => [.. _continuationReasonCodes];
        init => _continuationReasonCodes = value is null ? [] : [.. value];
    }

    /// <summary>Gets the standard pipeline failure when stage processing did not succeed.</summary>
    [Key(9)]
    public StrategyPipelineFailure? Failure { get; init; }

    /// <summary>Gets the immutable workflow revision supplied to this pipeline execution.</summary>
    [Key(10)]
    public long InputWorkflowRevision { get; init; }

    /// <summary>Gets the frozen pipeline parameter-set identity.</summary>
    [Key(11)]
    public Guid ParameterSetId { get; init; }

    /// <summary>Gets the frozen pipeline parameter-set version.</summary>
    [Key(12)]
    public int ParameterSetVersion { get; init; }

    /// <summary>Gets the canonical hash of the frozen pipeline parameter payload.</summary>
    [Key(13)]
    public string ParameterPayloadSha256 { get; init; } = string.Empty;

    /// <summary>Gets the fixed UTC deadline inherited by this pipeline execution.</summary>
    [Key(14)]
    public DateTime? ExpiresAtUtc { get; init; }

    /// <summary>Gets the accepted terminal pipeline notification identity for idempotency.</summary>
    [Key(15)]
    public Guid SourceEventId { get; init; }
}
