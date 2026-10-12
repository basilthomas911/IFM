using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Classifies the current read-only operational condition of a workflow entity.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
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
