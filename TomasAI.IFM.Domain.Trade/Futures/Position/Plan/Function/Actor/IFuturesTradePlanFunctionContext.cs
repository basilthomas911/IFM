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

    IEventSourceFunctionStateRepository<FuturesTradePlanFunctionState, UpdateFuturesTradePlanCommand> StateRepository { get; }
}
