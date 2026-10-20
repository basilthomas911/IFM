using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.Actor;
public interface IIronCondorForwardPriceFunctionContext : IFunctionActorContext<IronCondorForwardPriceFunctionActor>
{

    TimeProvider TimeProvider { get; }

    ILogger<IronCondorForwardPriceFunctionActor> Logger { get; }

    IEventSourceFunctionStateRepository<IronCondorForwardPriceFunctionState, CalculateIronCondorForwardPriceCommand> StateRepository { get; }
}
