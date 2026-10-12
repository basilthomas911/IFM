using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
public interface IIronCondorTradePlanFunctionContext : IFunctionActorContext<IronCondorTradePlanFunctionActor>
{
    IronCondorTradePlanAlgorithm Algorithm { get; }

    TimeProvider TimeProvider { get; }

    ILogger<IronCondorTradePlanFunctionActor> Logger { get; }

    IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand> StateRepository { get; }
}
