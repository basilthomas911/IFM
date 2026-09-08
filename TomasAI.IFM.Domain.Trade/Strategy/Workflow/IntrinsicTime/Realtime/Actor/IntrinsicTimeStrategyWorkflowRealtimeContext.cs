using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

/// <summary>Defines the readonly services owned by the Intrinsic Time Strategy Workflow Realtime actor.</summary>
public interface IIntrinsicTimeStrategyWorkflowRealtimeContext
    : IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>
{
    /// <summary>Gets the workflow clock.</summary>
    TimeProvider TimeProvider { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<IntrinsicTimeStrategyWorkflowRealtimeActor> Logger { get; }

    /// <summary>Gets the live-trigger feature options.</summary>
    IntrinsicTimeStrategyWorkflowOptions Options { get; }

    /// <summary>Gets the validated Regime Discovery hard-timeout options.</summary>
    RegimeDiscoveryExecutionOptions RegimeDiscoveryExecutionOptions { get; }

    /// <summary>Gets the immutable strategy-configuration store.</summary>
    IConfigurationDbContext ConfigurationDb { get; }
    IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository { get; }
    IPortfolioQueryApi PortfolioQueries {get;}
    IPortfolioFundCommandApi PortfolioCommands {get;}
    Application.MarketData.Pricing.ICompositionPreparationStore CompositionPreparations
        => throw new InvalidOperationException("Composition preparation storage is not configured.");
    Application.MarketData.Pricing.CompositionMarketPreparation CompositionMarketPreparation
        => throw new InvalidOperationException("Composition market preparation is not configured.");

    /// <summary>Gets the atomic signal snapshot provider used by the live-readiness gate.</summary>
    IRegimeDiscoveryMarketSignalSnapshotProvider RegimeDiscoverySnapshotProvider { get; }
}

/// <summary>Provides the closed-generic context for one-way workflow realtime orchestration.</summary>
public sealed class IntrinsicTimeStrategyWorkflowRealtimeContext
    : EventActorContext,
      IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>,
      IIntrinsicTimeStrategyWorkflowRealtimeContext
{
    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository => Container.Resolve<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
    public IPortfolioQueryApi PortfolioQueries => Container.Resolve<IPortfolioQueryApi>();
    public IPortfolioFundCommandApi PortfolioCommands => Container.Resolve<IPortfolioFundCommandApi>();
    public Application.MarketData.Pricing.ICompositionPreparationStore CompositionPreparations
        => Container.Resolve<Application.MarketData.Pricing.ICompositionPreparationStore>();
    public Application.MarketData.Pricing.CompositionMarketPreparation CompositionMarketPreparation
        => Container.Resolve<Application.MarketData.Pricing.CompositionMarketPreparation>();
    readonly Lazy<IConfigurationDbContext> _configurationDb;
    readonly Lazy<IRegimeDiscoveryMarketSignalSnapshotProvider> _regimeDiscoverySnapshotProvider;
    /// <summary>Initializes the realtime context.</summary>
    public IntrinsicTimeStrategyWorkflowRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<IntrinsicTimeStrategyWorkflowRealtimeActor> logger,
        IntrinsicTimeStrategyWorkflowOptions options,
        RegimeDiscoveryExecutionOptions regimeDiscoveryExecutionOptions)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName))
    {
        TimeProvider = TimeProvider.System;
        Logger = IsArgumentNull.Set(logger);
        Options = IsArgumentNull.Set(options);
        RegimeDiscoveryExecutionOptions = IsArgumentNull.Set(regimeDiscoveryExecutionOptions);
        _configurationDb = new(() => IsArgumentNull.Set(Container.Resolve<IConfigurationDbContext>())!);
        _regimeDiscoverySnapshotProvider = new(() => IsArgumentNull.Set(
            Container.Resolve<IRegimeDiscoveryMarketSignalSnapshotProvider>())!);
    }

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }

    /// <inheritdoc />
    public ILogger<IntrinsicTimeStrategyWorkflowRealtimeActor> Logger { get; }

    /// <inheritdoc />
    public IntrinsicTimeStrategyWorkflowOptions Options { get; }

    /// <inheritdoc />
    public RegimeDiscoveryExecutionOptions RegimeDiscoveryExecutionOptions { get; }

    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => _configurationDb.Value;

    /// <inheritdoc />
    public IRegimeDiscoveryMarketSignalSnapshotProvider RegimeDiscoverySnapshotProvider =>
        _regimeDiscoverySnapshotProvider.Value;
}

/// <summary>Controls live automatic ITI-trigger routing for the workflow skeleton.</summary>
public sealed class IntrinsicTimeStrategyWorkflowOptions
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

    /// <summary>Gets or sets the fund used by the configured Intrinsic Time workflow.</summary>
    public int FundId { get; set; } = 1;
    public WorkflowActivationReference[] Activations {get;set;} = [];
}

public sealed record WorkflowActivationReference(TimeFrameType Horizon,Guid Id,int Version,string PayloadSha256);
