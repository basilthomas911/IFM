using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function;

public static class UpdateVerticalSpreadTradePlan
{
    public static ValueTask<FunctionResult<VerticalSpreadTradePlanUpdatedEvent,
        TradePlanFailedEvent<VerticalSpreadTradePlanId>>> ExecuteAsync(
        this UpdateVerticalSpreadTradePlanCommand command,
        VerticalSpreadTradePlanFunctionState state,
        IVerticalSpreadTradePlanFunctionContext context,
        Func<FunctionEventContext<UpdateVerticalSpreadTradePlanCommand>,
            FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>> dispatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var calculated = context.Algorithm.Calculate(command.Position, command.Parameters,
            state.PreviousPlan, command.EntityId.ValueDate, context.TimeProvider.GetUtcNow().UtcDateTime).Snapshot;
        return ValueTask.FromResult(dispatch(new(typeof(VerticalSpreadTradePlanUpdatedEvent), command, calculated)));
    }
}
