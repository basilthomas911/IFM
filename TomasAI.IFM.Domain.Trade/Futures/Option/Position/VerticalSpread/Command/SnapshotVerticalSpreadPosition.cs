using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command;

public static class SnapshotVerticalSpreadPosition
{
    public static ServiceResult<GuidResult> Execute(
        this SnapshotVerticalSpreadPositionCommand command,
        VerticalSpreadPositionCommandState state) =>
        state.Current is not null
            ? TradeCommandResult.Accepted(command.CommandId)
            : new ServiceFailed<GuidResult>(command.ErrorCode, "POSITION.NOT_FOUND");
}
