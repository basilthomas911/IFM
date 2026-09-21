using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

public static class CloseIronCondorPosition
{
    public static ServiceResult<GuidResult> Execute(
        this CloseIronCondorPositionCommand command,
        IronCondorPositionCommandState state) =>
        IronCondorPositionTransition.Apply(
            command,
            state,
            machine => command.ClosingFills.Length == 0 ? machine.Close(command.EffectiveAtUtc)
                : machine.Close(command.ClosingFills, command.EffectiveAtUtc));
}
