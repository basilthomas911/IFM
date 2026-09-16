using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Model;

internal static class VerticalSpreadPositionTransition
{
    internal static ServiceResult<GuidResult> Apply(
        ICommand<StrategyPositionId> command,
        VerticalSpreadPositionCommandState state,
        Func<StrategyPositionActorStateMachine, TradeDecision<StrategyPositionSnapshot>> transition)
    {
        var machine = new StrategyPositionActorStateMachine();
        if (state.Current is { } current)
            machine.Replay(current);

        var decision = transition(machine);
        if (!decision.Accepted || decision.Value is null)
            return TradeCommandResult.Rejected(command.ErrorCode, decision);

        if (!state.Update(new VerticalSpreadPositionChangedEvent
            {
                EntityId = command.EntityId,
                State = decision.Value
            }, command))
            return new ServiceFailed<GuidResult>(command.ErrorCode, "POSITION.STATE.APPLY_FAILED");
        return TradeCommandResult.Accepted(command.CommandId);
    }
}
