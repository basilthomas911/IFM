using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command;

public static class SnapshotFuturesPosition
{
    /// <summary>
    /// Confirms that resident futures-position state exists so the base actor can flush its window.
    /// </summary>
    /// <param name="command">The snapshot command.</param>
    /// <param name="state">The resident event-sourced futures-position state.</param>
    /// <returns>A successful acknowledgement when state exists; otherwise, a not-found failure.</returns>
    public static ServiceResult<GuidResult> Execute(
        this SnapshotFuturesPositionCommand command,
        FuturesPositionCommandState state) =>
        state.Current is not null
            ? TradeCommandResult.Accepted(command.CommandId)
            : command.UpdateFailed("POSITION.NOT_FOUND");
}
