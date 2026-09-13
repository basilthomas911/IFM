using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

public static class SnapshotIronCondorPosition
{
    public static ServiceResult<GuidResult> Execute(
        this SnapshotIronCondorPositionCommand command,
        IronCondorPositionCommandState state) =>
        state.Current is not null
            ? TradeCommandResult.Accepted(command.CommandId)
            : new ServiceFailed<GuidResult>(command.ErrorCode, "POSITION.NOT_FOUND");
}
