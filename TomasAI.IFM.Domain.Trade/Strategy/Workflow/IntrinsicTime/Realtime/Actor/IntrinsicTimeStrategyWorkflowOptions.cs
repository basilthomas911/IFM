using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
/// <summary>Controls live automatic ITI-trigger routing for the workflow skeleton.</summary>
public sealed class IntrinsicTimeStrategyWorkflowOptions : TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.IIntrinsicTimeWorkflowStartPolicy
{
    /// <summary>Gets or sets whether live ITI triggers may start workflow executions.</summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Gets or sets whether every required Regime Discovery signal must pass an atomic snapshot preflight before a
    /// live ITI trigger can start a workflow.
    /// </summary>
    public bool RequireWarmRegimeDiscoverySignals { get; set; } = true;
    /// <summary>Market profile shared across strategy families, resolved for the triggering timeframe.</summary>
    public string MarketConditionAssessmentProfileId { get; set; } = "ES.Standard";
    /// <summary>
    /// Gets or sets whether the Development host provisions the assessment profiles required by realtime workflows.
    /// This setting is ignored outside the Development environment.
    /// </summary>
    public bool ProvisionDevelopmentMarketConditionAssessmentDefaults { get; set; }
    /// <summary>Gets or sets the fund used by the configured Intrinsic Time workflow.</summary>
    public int FundId { get; set; } = 1;
    /// <summary>Portfolio authority consulted only at the RiskManager boundary.</summary>
    public int PortfolioId { get; set; } = 1;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retain the established array-valued configuration binder contract.")]
    public WorkflowActivationReference[] Activations { get; set; } = [];
}
