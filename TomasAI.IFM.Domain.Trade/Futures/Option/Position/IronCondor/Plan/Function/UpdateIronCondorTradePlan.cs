using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;

public static class UpdateIronCondorTradePlan
{
    public static ValueTask<FunctionResult<IronCondorTradePlanUpdatedEvent,
        TradePlanFailedEvent<IronCondorTradePlanId>>> ExecuteAsync(
        this UpdateIronCondorTradePlanCommand command,
        IronCondorTradePlanFunctionState state,
        IIronCondorTradePlanFunctionContext context,
        Func<FunctionEventContext<UpdateIronCondorTradePlanCommand>,
            FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>> dispatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var calculated = context.Algorithm.Calculate(command.Position, command.Parameters,
            state.PreviousPlan, command.EntityId.ValueDate, context.TimeProvider.GetUtcNow().UtcDateTime).Snapshot;
        return ValueTask.FromResult(dispatch(new(typeof(IronCondorTradePlanUpdatedEvent), command, calculated)));
    }
}
