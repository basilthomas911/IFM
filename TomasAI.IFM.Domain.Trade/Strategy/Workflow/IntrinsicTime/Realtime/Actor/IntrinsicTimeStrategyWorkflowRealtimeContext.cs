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
/// <summary>Provides the closed-generic context for one-way workflow realtime orchestration.</summary>
public sealed class IntrinsicTimeStrategyWorkflowRealtimeContext : EventActorContext, IIntrinsicTimeStrategyWorkflowRealtimeContext
{
    public IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository => Container.Resolve<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
    public Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi FinancialApi => Container.Resolve<Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>();
    public IPortfolioQueryApi PortfolioQueries => Container.Resolve<IPortfolioQueryApi>();
    public IPortfolioFundCommandApi PortfolioCommands => Container.Resolve<IPortfolioFundCommandApi>();
    public IPortfolioOrderCompositionApi PortfolioOrderComposition => Container.Resolve<IPortfolioOrderCompositionApi>();
    public Application.MarketData.Pricing.ICompositionPreparationStore CompositionPreparations => Container.Resolve<Application.MarketData.Pricing.ICompositionPreparationStore>();
    public Application.MarketData.Pricing.CompositionMarketPreparation CompositionMarketPreparation => Container.Resolve<Application.MarketData.Pricing.CompositionMarketPreparation>();
    public Domain.MarketData.Shared.IValueDateProvider ValueDateProvider => Container.Resolve<Domain.MarketData.Shared.IValueDateProvider>();
    public Application.MarketData.Contracts.IMarketDataApi MarketDataApi => Container.Resolve<Application.MarketData.Contracts.IMarketDataApi>();

    readonly Lazy<IConfigurationDbContext> _configurationDb;
    readonly Lazy<IRegimeDiscoveryMarketSignalSnapshotProvider> _regimeDiscoverySnapshotProvider;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    /// <summary>Initializes the realtime context.</summary>
    public IntrinsicTimeStrategyWorkflowRealtimeContext(IActorSupervisor supervisor, ILogger<IntrinsicTimeStrategyWorkflowRealtimeActor> logger, IntrinsicTimeStrategyWorkflowOptions options, RegimeDiscoveryExecutionOptions regimeDiscoveryExecutionOptions) : base(supervisor, new ActorMailboxId(ActorType.Realtime, IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName))
    {
        TimeProvider = TimeProvider.System;
        Logger = IsArgumentNull.Set(logger);
        Options = IsArgumentNull.Set(options);
        RegimeDiscoveryExecutionOptions = IsArgumentNull.Set(regimeDiscoveryExecutionOptions);
        _configurationDb = new(() => IsArgumentNull.Set(Container.Resolve<IConfigurationDbContext>())!);
        _regimeDiscoverySnapshotProvider = new(() => IsArgumentNull.Set(Container.Resolve<IRegimeDiscoveryMarketSignalSnapshotProvider>())!);
    }

    /// <inheritdoc/>
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc/>
    public ILogger<IntrinsicTimeStrategyWorkflowRealtimeActor> Logger { get; }
    /// <inheritdoc/>
    public IntrinsicTimeStrategyWorkflowOptions Options { get; }
    /// <inheritdoc/>
    public RegimeDiscoveryExecutionOptions RegimeDiscoveryExecutionOptions { get; }
    /// <inheritdoc/>
    public TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterRuntimeSnapshot ParameterRuntime => Container.Resolve<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterRuntimeSnapshot>();
    public IConfigurationDbContext ConfigurationDb => _configurationDb.Value;
    /// <inheritdoc/>
    public IRegimeDiscoveryMarketSignalSnapshotProvider RegimeDiscoverySnapshotProvider => _regimeDiscoverySnapshotProvider.Value;
}
