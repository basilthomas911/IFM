using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;

using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;

public interface ITradeSelectionFunctionContext : IFunctionActorContext<TradeSelectionFunctionActor>
{
    IStrategyCatalogCapabilities Capabilities {get;}
    TimeProvider TimeProvider { get; }
    ILogger<TradeSelectionFunctionActor> Logger { get; }
    IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand> StateRepository { get; }
    IFunctionProjector<TradeSelectionFunctionCompletedEvent> FunctionProjector { get; }
}

public sealed class TradeSelectionFunctionContext : FunctionActorContext,
    IFunctionActorContext<TradeSelectionFunctionActor>, ITradeSelectionFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand>> _repository;
    readonly Lazy<IFunctionProjector<TradeSelectionFunctionCompletedEvent>> _projector;
    public TradeSelectionFunctionContext(IActorSupervisor supervisor,
        ILogger<TradeSelectionFunctionActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Function, TradeSelectionFunctionActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger); TimeProvider = TimeProvider.System;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand>>());
        _projector = new(() => Container.Resolve<IFunctionProjector<TradeSelectionFunctionCompletedEvent>>());
    }
    public ILogger<TradeSelectionFunctionActor> Logger { get; }
    public IStrategyCatalogCapabilities Capabilities => Container.Resolve<IStrategyCatalogCapabilities>();
    public TimeProvider TimeProvider { get; }
    public IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand> StateRepository => _repository.Value;
    public IFunctionProjector<TradeSelectionFunctionCompletedEvent> FunctionProjector => _projector.Value;
}
