using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;

/// <summary>Defines dependencies owned by the completed-only Regime Discovery Function actor.</summary>
public interface IRegimeDiscoveryFunctionContext : IFunctionActorContext<RegimeDiscoveryFunctionActor>
{
    IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand>
        StateRepository { get; }
    IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent> FunctionProjector { get; }
    IRegimeDiscoveryMarketSignalSnapshotProvider SnapshotProvider { get; }
    IRegimeDiscoveryCalculationModel CalculationModel { get; }
    RegimeDiscoveryExecutionMode ExecutionMode { get; }
    TimeProvider TimeProvider { get; }
    ILogger<RegimeDiscoveryFunctionActor> Logger { get; }
}

/// <summary>Provides the closed-generic Function context for Regime Discovery.</summary>
public sealed class RegimeDiscoveryFunctionContext
    : FunctionActorContext,
      IFunctionActorContext<RegimeDiscoveryFunctionActor>,
      IRegimeDiscoveryFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<
        RegimeDiscoveryFunctionState,
        ExecuteRegimeDiscoveryPipelineCommand>> _stateRepository;
    readonly Lazy<IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent>> _functionProjector;
    readonly Lazy<IRegimeDiscoveryMarketSignalSnapshotProvider> _snapshotProvider;

    public RegimeDiscoveryFunctionContext(
        IActorSupervisor supervisor,
        ILogger<RegimeDiscoveryFunctionActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Function, RegimeDiscoveryFunctionActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger);
        TimeProvider = TimeProvider.System;
        CalculationModel = new RegimeDiscoveryCalculationModel();
        ExecutionMode = RegimeDiscoveryExecutionMode.Sequential;
        _stateRepository = ResolveOnce<IEventSourceFunctionStateRepository<
            RegimeDiscoveryFunctionState,
            ExecuteRegimeDiscoveryPipelineCommand>>();
        _functionProjector = ResolveOnce<IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent>>();
        _snapshotProvider = ResolveOnce<IRegimeDiscoveryMarketSignalSnapshotProvider>();
    }

    public ILogger<RegimeDiscoveryFunctionActor> Logger { get; }
    public TimeProvider TimeProvider { get; }
    public IRegimeDiscoveryCalculationModel CalculationModel { get; }
    public RegimeDiscoveryExecutionMode ExecutionMode { get; }
    public IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent> FunctionProjector
        => _functionProjector.Value;
    public IEventSourceFunctionStateRepository<RegimeDiscoveryFunctionState, ExecuteRegimeDiscoveryPipelineCommand>
        StateRepository => _stateRepository.Value;
    public IRegimeDiscoveryMarketSignalSnapshotProvider SnapshotProvider => _snapshotProvider.Value;

    Lazy<TService> ResolveOnce<TService>() where TService : class
        => new(() => IsArgumentNull.Set(Container.Resolve<TService>())!);
}
