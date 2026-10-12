using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;

public interface ICapacityReservationFunctionContext : IFunctionActorContext<CapacityReservationFunctionActor>
{
    IEventSourceFunctionStateRepository<CapacityReservationFunctionState, ReservePortfolioTradeRiskCommand> StateRepository { get; }
    TimeProvider TimeProvider { get; }
    ILogger<CapacityReservationFunctionActor> Logger { get; }
}

