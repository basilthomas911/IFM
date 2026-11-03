using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command;

public static class SnapshotVerticalSpreadPosition
{

    /// <param name="command">
    /// The position snapshot request.
    /// </param>
    extension(SnapshotVerticalSpreadPositionCommand command)
    {
        /// <summary>Acknowledges a resident position so the actor can flush its persistence window.</summary>
        ///
        /// <param name = "state">The authoritative resident position state.</param>
        /// <returns>Acceptance when a position exists, otherwise its business rejection.</returns>
        public ServiceResult<GuidResult> Execute(VerticalSpreadPositionCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            return state.PositionSnapshot is not null ? TradeCommandResult.Accepted(command.CommandId) : command.UpdateFailed("VerticalSpreadPosition.NOT_FOUND");
        }
    }
}
