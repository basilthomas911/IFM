using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command;

public static class CloseVerticalSpreadPosition
{
    public static ServiceResult<GuidResult> Execute(
        this CloseVerticalSpreadPositionCommand command,
        VerticalSpreadPositionCommandState state) =>
        VerticalSpreadPositionTransition.Apply(
            command,
            state,
            machine => command.ClosingFills.Length == 0 ? machine.Close(command.EffectiveAtUtc)
                : machine.Close(command.ClosingFills, command.EffectiveAtUtc));
}
