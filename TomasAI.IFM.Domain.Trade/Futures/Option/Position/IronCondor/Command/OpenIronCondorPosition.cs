using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

public static class OpenIronCondorPosition
{
    public static ServiceResult<GuidResult> Execute(
        this OpenIronCondorPositionCommand command,
        IronCondorPositionCommandState state) =>
        IronCondorPositionTransition.Apply(
            command,
            state,
            machine => machine.Open(command.Trade, command.EntityId.PositionId, command.EffectiveAtUtc));
}
