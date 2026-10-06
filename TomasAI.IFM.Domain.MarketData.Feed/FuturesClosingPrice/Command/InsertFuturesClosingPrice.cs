using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command;

/// <summary>Handles InsertFuturesClosingPrice commands through computation and state-owned event application.</summary>
public static class InsertFuturesClosingPrice
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this InsertFuturesClosingPriceCommand command, FuturesClosingPriceCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesClosingPriceInsertedEvent";
        var updated = command.Compute(state.FuturesClosingPriceExists(command.FuturesClosingPriceId), out var futuresClosingPriceInsertion) switch
        {
            _ when !futuresClosingPriceInsertion.Accepted
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: futures {command.FuturesClosingPriceId.ContractId} closing price for {command.FuturesClosingPriceId.ValueDate:yyyy-MM-dd} already exists"),
            _ => state.Update(command.CreateFuturesClosingPriceInsertedEvent(futuresClosingPriceInsertion), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="futuresClosingPriceExists">Whether the owning state already contains this closing price.</param>
    /// <param name="futuresClosingPriceInsertion">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    internal static bool Compute(this InsertFuturesClosingPriceCommand command, bool futuresClosingPriceExists, out FuturesClosingPriceInsertion futuresClosingPriceInsertion)
    {
        futuresClosingPriceInsertion = new(command.FuturesClosingPriceId, command.ClosingPrice, !futuresClosingPriceExists);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="futuresClosingPriceInsertion">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static FuturesClosingPriceInsertedEvent CreateFuturesClosingPriceInsertedEvent(this InsertFuturesClosingPriceCommand command, FuturesClosingPriceInsertion futuresClosingPriceInsertion)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesClosingPriceInsertedEvent.Actor, FuturesClosingPriceInsertedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            FuturesClosingPriceId = futuresClosingPriceInsertion.FuturesClosingPriceId,
            ClosingPrice = futuresClosingPriceInsertion.ClosingPrice,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
