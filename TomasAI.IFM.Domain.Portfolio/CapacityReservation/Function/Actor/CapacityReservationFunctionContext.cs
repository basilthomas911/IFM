using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;

public interface ICapacityReservationFunctionContext : IFunctionActorContext<CapacityReservationFunctionActor>
{
    IEventSourceFunctionStateRepository<CapacityReservationFunctionState,ReservePortfolioTradeRiskCommand> StateRepository { get; }
    TimeProvider TimeProvider { get; }
    ILogger<CapacityReservationFunctionActor> Logger { get; }
}

/// <summary>The typed and closed-generic interfaces share one singleton registration.</summary>
public sealed class CapacityReservationFunctionContext : FunctionActorContext,ICapacityReservationFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<CapacityReservationFunctionState,ReservePortfolioTradeRiskCommand>> _repository;
    public CapacityReservationFunctionContext(IActorSupervisor supervisor,ILogger<CapacityReservationFunctionActor> logger)
        :base(supervisor,new ActorMailboxId(ActorType.Function,CapacityReservationFunctionActor.ActorName))
    {
        Logger=logger;
        _repository=new(()=>Container.Resolve<IEventSourceFunctionStateRepository<CapacityReservationFunctionState,ReservePortfolioTradeRiskCommand>>()
            ??throw new InvalidOperationException("Financial repository is not registered."));
    }
    public IEventSourceFunctionStateRepository<CapacityReservationFunctionState,ReservePortfolioTradeRiskCommand> StateRepository=>_repository.Value;
    public TimeProvider TimeProvider=>TimeProvider.System;
    public ILogger<CapacityReservationFunctionActor> Logger { get; }
}

