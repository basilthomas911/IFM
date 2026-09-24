using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function;

public static class UpdateFuturesTradePlan
{
    public static ValueTask<FunctionResult<FuturesTradePlanUpdatedEvent,
        TradePlanFailedEvent<FuturesTradePlanId>>> ExecuteAsync(
        this UpdateFuturesTradePlanCommand command,
        FuturesTradePlanFunctionState state,
        IFuturesTradePlanFunctionContext context,
        Func<FunctionEventContext<UpdateFuturesTradePlanCommand>,
            FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>> dispatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var calculated = context.Algorithm.Calculate(command.Position, command.Parameters,
            state.PreviousPlan, command.EntityId.ValueDate, context.TimeProvider.GetUtcNow().UtcDateTime).Snapshot;
        return ValueTask.FromResult(dispatch(new(typeof(FuturesTradePlanUpdatedEvent), command, calculated)));
    }
}
