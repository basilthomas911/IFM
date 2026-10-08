using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

public static class SnapshotIronCondorPosition
{
    /// <summary>Acknowledges a resident position so the actor can flush its persistence window.</summary>
    /// <param name="command">The position snapshot request.</param>
    /// <param name="state">The authoritative resident position state.</param>
    /// <returns>Acceptance when a position exists, otherwise its business rejection.</returns>
    public static ServiceResult<GuidResult> Execute(
        this SnapshotIronCondorPositionCommand command,
        IronCondorPositionCommandState state) =>
        state.PositionSnapshot is not null
            ? TradeCommandResult.Accepted(command.CommandId)
            : command.UpdateFailed("IronCondorPosition.NOT_FOUND");
}
