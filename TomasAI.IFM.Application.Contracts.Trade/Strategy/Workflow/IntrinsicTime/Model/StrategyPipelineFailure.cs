using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Describes a standard failure returned by a strategy workflow pipeline.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyPipelineFailure
{
    /// <summary>Gets the stable application error code.</summary>
    [Key(0)]
    public int ErrorCode { get; init; }

    /// <summary>Gets the safe failure message.</summary>
    [Key(1)]
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>Gets the stable logical failure type.</summary>
    [Key(2)]
    public string ErrorType { get; init; } = string.Empty;

    /// <summary>Gets optional structured or diagnostic failure data.</summary>
    [Key(3)]
    public string ErrorData { get; init; } = string.Empty;

    /// <summary>Gets the UTC timestamp at which the pipeline failed.</summary>
    [Key(4)]
    public DateTime FailedAtUtc { get; init; }
}
