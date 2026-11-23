using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function;

public static class UpdateFuturesTradePlan
{

    extension(UpdateFuturesTradePlanCommand command)
    {
        public ValueTask<FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>> ExecuteAsync(FuturesTradePlanFunctionState state, IFuturesTradePlanFunctionContext context, Func<FunctionEventContext<UpdateFuturesTradePlanCommand>, FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>> dispatch, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(dispatch);
            ArgumentNullException.ThrowIfNull(state);
            cancellationToken.ThrowIfCancellationRequested();
            // Clock notifications can race a newer committed market update.
            // Never replace a newer observed position with the older clock copy.
            var position = state.PreviousPlan is { } previous && previous.Position.Id == command.Position.Id
                && previous.Position.PositionSequence > command.Position.PositionSequence ? previous.Position : command.Position;
            var calculated = context.Algorithm.Calculate(position, command.Parameters, state.PreviousPlan, command.EntityId.ValueDate, context.TimeProvider.GetUtcNow().UtcDateTime).Snapshot;
            return ValueTask.FromResult(dispatch(new(typeof(FuturesTradePlanUpdatedEvent), command, calculated)));
        }
    }
}
