using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;
public interface IVerticalSpreadTradePlanFunctionContext : IFunctionActorContext<VerticalSpreadTradePlanFunctionActor>
{
    VerticalSpreadTradePlanAlgorithm Algorithm { get; }

    TimeProvider TimeProvider { get; }

    ILogger<VerticalSpreadTradePlanFunctionActor> Logger { get; }

    IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand> StateRepository { get; }
}
