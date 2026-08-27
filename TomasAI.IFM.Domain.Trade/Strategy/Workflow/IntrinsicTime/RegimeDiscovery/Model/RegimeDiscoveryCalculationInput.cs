using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Contains all immutable inputs required for one deterministic Regime Discovery calculation.</summary>
public sealed record RegimeDiscoveryCalculationInput
{
    /// <summary>Gets the preselected result identity.</summary>
    public Guid ResultId { get; init; }
    /// <summary>Gets the owning workflow execution.</summary>
    public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the workflow routing identity.</summary>
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; }
    /// <summary>Gets the triggering ITI event identity.</summary>
    public Guid TriggerEventId { get; init; }
    /// <summary>Gets the complete frozen parameter set.</summary>
    public RegimeDiscoveryParameterSet ParameterSet { get; init; } = new();
    /// <summary>Gets one revision-stable market-signal snapshot.</summary>
    public RegimeDiscoveryMarketSignalSnapshot Snapshot { get; init; } = new();
    /// <summary>Gets the deterministic UTC result production timestamp.</summary>
    public DateTime ProducedAtUtc { get; init; }
}

/// <summary>Identifies how independent specialist calculations are scheduled.</summary>
public enum RegimeDiscoveryExecutionMode : byte
{
    /// <summary>Specialists execute deterministically on the current actor task.</summary>
    Sequential = 0,
    /// <summary>Specialists execute on ordinary .NET thread-pool tasks and are awaited together.</summary>
    ThreadPoolParallel = 1
}
