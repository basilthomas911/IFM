using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;

public interface IFuturesTradePlanFunctionContext : IFunctionActorContext<FuturesTradePlanFunctionActor>
{
    FuturesTradePlanAlgorithm Algorithm { get; }
    TimeProvider TimeProvider { get; }
    ILogger<FuturesTradePlanFunctionActor> Logger { get; }
    IEventSourceFunctionStateRepository<FuturesTradePlanFunctionState,
        UpdateFuturesTradePlanCommand> StateRepository { get; }
}

public sealed class FuturesTradePlanFunctionContext : FunctionActorContext,
    IFunctionActorContext<FuturesTradePlanFunctionActor>, IFuturesTradePlanFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<FuturesTradePlanFunctionState,
        UpdateFuturesTradePlanCommand>> _repository;

    public FuturesTradePlanFunctionContext(IActorSupervisor supervisor,
        ILogger<FuturesTradePlanFunctionActor> logger)
        : base(supervisor, new(ActorType.Function, FuturesTradePlanFunctionActor.ActorName))
    {
        Logger = logger;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<
            FuturesTradePlanFunctionState, UpdateFuturesTradePlanCommand>>());
    }

    public FuturesTradePlanAlgorithm Algorithm { get; } = new();
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public ILogger<FuturesTradePlanFunctionActor> Logger { get; }
    public IEventSourceFunctionStateRepository<FuturesTradePlanFunctionState,
        UpdateFuturesTradePlanCommand> StateRepository => _repository.Value;
}
