using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;

using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;

public interface IOrderCompositionFunctionContext : IFunctionActorContext<OrderCompositionFunctionActor>
{
    IOrderComposer CalculationModel { get; }
    TimeProvider TimeProvider { get; }
    ILogger<OrderCompositionFunctionActor> Logger { get; }
    IEventSourceFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand> StateRepository { get; }
    IFunctionProjector<OrderCompositionFunctionCompletedEvent> FunctionProjector { get; }
}

public sealed class OrderCompositionFunctionContext : FunctionActorContext,
    IFunctionActorContext<OrderCompositionFunctionActor>, IOrderCompositionFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand>> _repository;
    readonly Lazy<IFunctionProjector<OrderCompositionFunctionCompletedEvent>> _projector;
    public OrderCompositionFunctionContext(IActorSupervisor supervisor,
        ILogger<OrderCompositionFunctionActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Function, OrderCompositionFunctionActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger); TimeProvider = TimeProvider.System;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand>>());
        _projector = new(() => Container.Resolve<IFunctionProjector<OrderCompositionFunctionCompletedEvent>>());
    }
    public IOrderComposer CalculationModel { get; } = new Model.OrderComposer(new Black76ComposerPricer());
    public ILogger<OrderCompositionFunctionActor> Logger { get; }
    public TimeProvider TimeProvider { get; }
    public IEventSourceFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand> StateRepository => _repository.Value;
    public IFunctionProjector<OrderCompositionFunctionCompletedEvent> FunctionProjector => _projector.Value;
}
