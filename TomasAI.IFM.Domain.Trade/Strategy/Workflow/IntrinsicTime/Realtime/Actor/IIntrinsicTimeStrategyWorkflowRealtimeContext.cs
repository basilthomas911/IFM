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
/// <summary>Defines the readonly services owned by the Intrinsic Time Strategy Workflow Realtime actor.</summary>
public interface IIntrinsicTimeStrategyWorkflowRealtimeContext : IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>
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

    TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterRuntimeSnapshot? ParameterRuntime => null;

    IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState> WorkflowRepository { get; }

    Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi FinancialApi => throw new InvalidOperationException("Financial API is not configured.");

    IPortfolioQueryApi PortfolioQueries { get; }

    IPortfolioFundCommandApi PortfolioCommands { get; }

    IPortfolioOrderCompositionApi PortfolioOrderComposition { get; }

    Application.MarketData.Pricing.ICompositionPreparationStore CompositionPreparations => throw new InvalidOperationException("Composition preparation storage is not configured.");

    Application.MarketData.Pricing.CompositionMarketPreparation CompositionMarketPreparation => throw new InvalidOperationException("Composition market preparation is not configured.");

    /// <summary>Accepted operational value date advanced only by successful market close.</summary>
    Domain.MarketData.Shared.IValueDateProvider ValueDateProvider => throw new InvalidOperationException("Operational value date is not configured.");

    /// <summary>Single application boundary for immediate prepared market reads.</summary>
    Application.MarketData.Contracts.IMarketDataApi MarketDataApi => throw new InvalidOperationException("Market data API is not configured.");

    /// <summary>Gets the atomic signal snapshot provider used by the live-readiness gate.</summary>
    IRegimeDiscoveryMarketSignalSnapshotProvider RegimeDiscoverySnapshotProvider { get; }
}
