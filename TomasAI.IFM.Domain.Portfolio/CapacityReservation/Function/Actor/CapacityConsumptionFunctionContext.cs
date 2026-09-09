using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;

public interface ICapacityConsumptionFunctionContext : IFunctionActorContext<CapacityConsumptionFunctionActor>
{
    IEventSourceFunctionStateRepository<CapacityConsumptionFunctionState,ConsumeCapacityReservationCommand> StateRepository { get; }
    TimeProvider TimeProvider { get; }
    ILogger<CapacityConsumptionFunctionActor> Logger { get; }
}

/// <summary>The typed and closed-generic interfaces share one singleton registration.</summary>
public sealed class CapacityConsumptionFunctionContext : FunctionActorContext,ICapacityConsumptionFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<CapacityConsumptionFunctionState,ConsumeCapacityReservationCommand>> _repository;
    public CapacityConsumptionFunctionContext(IActorSupervisor supervisor,ILogger<CapacityConsumptionFunctionActor> logger)
        :base(supervisor,new ActorMailboxId(ActorType.Function,CapacityConsumptionFunctionActor.ActorName))
    {
        Logger=logger;
        _repository=new(()=>Container.Resolve<IEventSourceFunctionStateRepository<CapacityConsumptionFunctionState,ConsumeCapacityReservationCommand>>()
            ??throw new InvalidOperationException("Financial repository is not registered."));
    }
    public IEventSourceFunctionStateRepository<CapacityConsumptionFunctionState,ConsumeCapacityReservationCommand> StateRepository=>_repository.Value;
    public TimeProvider TimeProvider=>TimeProvider.System;
    public ILogger<CapacityConsumptionFunctionActor> Logger { get; }
}

