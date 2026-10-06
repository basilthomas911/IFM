using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command;

/// <summary>Handles InsertVixFuturesEodData commands through computation and state-owned event application.</summary>
public static class InsertVixFuturesEodData
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this InsertVixFuturesEodDataCommand command, FuturesEodDataCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply VixFuturesEodDataInsertedEvent";
        var updated = command.Compute(out var vixFuturesEodDataInsertion) switch
        {
            _ => state.Update(command.CreateVixFuturesEodDataInsertedEvent(vixFuturesEodDataInsertion), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="vixFuturesEodDataInsertion">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    internal static bool Compute(this InsertVixFuturesEodDataCommand command, out VixFuturesEodDataInsertion vixFuturesEodDataInsertion)
    {
        vixFuturesEodDataInsertion = new(command.VixFuturesTickData);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="vixFuturesEodDataInsertion">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static VixFuturesEodDataInsertedEvent CreateVixFuturesEodDataInsertedEvent(this InsertVixFuturesEodDataCommand command, VixFuturesEodDataInsertion vixFuturesEodDataInsertion)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, VixFuturesEodDataInsertedEvent.Actor, VixFuturesEodDataInsertedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            VixFuturesTickData = vixFuturesEodDataInsertion.VixFuturesTickData,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
