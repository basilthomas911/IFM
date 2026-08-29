using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Classifies the current read-only operational condition of a workflow entity.</summary>
public enum IntrinsicTimeStrategyWorkflowOperationalStatus : byte
{
    /// <summary>No workflow snapshot has been committed for the entity.</summary>
    NotStarted = 0,
    /// <summary>The authoritative workflow is running inside its fixed deadline.</summary>
    Running = 1,
    /// <summary>The authoritative workflow is still Started but its fixed deadline is past.</summary>
    ExpiredNotClosed = 2,
    /// <summary>The workflow committed a non-timeout failure.</summary>
    Failed = 3,
    /// <summary>The workflow committed a timeout.</summary>
    TimedOut = 4,
    /// <summary>The workflow completed every required stage.</summary>
    Completed = 5,
    /// <summary>The workflow was explicitly cancelled.</summary>
    Cancelled = 6,
    /// <summary>A legacy event stream exists but cannot be reconstructed from an authoritative snapshot.</summary>
    MigrationBlocked = 7
}

/// <summary>
/// Combines the authoritative workflow snapshot with the last Regime terminal projection without mutating either.
/// </summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategyWorkflowObservationReadModel
{
    [Key(0)] public string WorkflowEntityId { get; init; } = string.Empty;
    [Key(1)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public WorkflowStrategyMachineStatus MachineStatus { get; init; }
    [Key(4)] public StrategyWorkflowStage CurrentStage { get; init; }
    [Key(5)] public long WorkflowRevision { get; init; }
    [Key(6)] public DateTime StartedAtUtc { get; init; }
    [Key(7)] public DateTime ExpiresAtUtc { get; init; }
    [Key(8)] public DateTime? TerminalAtUtc { get; init; }
    [Key(9)] public string StopReasonCode { get; init; } = string.Empty;
    [Key(10)] public IntrinsicTimeStrategyWorkflowOperationalStatus OperationalStatus { get; init; }
    [Key(11)] public bool IsOperationalIssue { get; init; }
    [Key(12)] public RegimeDiscoveryReadModel? RegimeTerminal { get; init; }
    [Key(13)] public bool WorkflowAcceptedRegimeTerminal { get; init; }
    [Key(14)] public bool NotificationLossSuspected { get; init; }
    [Key(15)] public DateTime ObservedAtUtc { get; init; }
    [Key(16)] public string Diagnostic { get; init; } = string.Empty;
    /// <summary>Gets the projected Market Condition terminal, including operational/UI evidence.</summary>
    [Key(17)] public MarketConditionReadModel? MarketConditionTerminal { get; init; }
    [Key(18)] public bool WorkflowAcceptedMarketConditionTerminal { get; init; }
    [Key(19)] public bool MarketConditionNotificationLossSuspected { get; init; }
}
