using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

public static class EndOfDayIronCondorPosition
{
    public static ServiceResult<GuidResult> Execute(
        this EndOfDayIronCondorPositionCommand command,
        IronCondorPositionCommandState state) =>
        IronCondorPositionTransition.Apply(
            command,
            state,
            machine => machine.EndOfDay(command.EffectiveAtUtc));
}
