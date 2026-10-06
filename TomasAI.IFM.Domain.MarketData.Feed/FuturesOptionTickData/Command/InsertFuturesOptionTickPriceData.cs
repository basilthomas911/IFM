using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;

/// <summary>Handles InsertFuturesOptionTickPriceData commands through computation and state-owned event application.</summary>
public static class InsertFuturesOptionTickPriceData
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this InsertFuturesOptionTickPriceDataCommand command, FuturesOptionTickDataCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesOptionTickPriceDataInsertedEvent";
        var updated = command.Compute(out var futuresOptionTickPriceDataInsertion) switch
        {
            _ => state.Update(command.CreateFuturesOptionTickPriceDataInsertedEvent(futuresOptionTickPriceDataInsertion), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="futuresOptionTickPriceDataInsertion">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    internal static bool Compute(this InsertFuturesOptionTickPriceDataCommand command, out FuturesOptionTickPriceDataInsertion futuresOptionTickPriceDataInsertion)
    {
        futuresOptionTickPriceDataInsertion = new(command.Contract, command.OptionTickData);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="futuresOptionTickPriceDataInsertion">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static FuturesOptionTickPriceDataInsertedEvent CreateFuturesOptionTickPriceDataInsertedEvent(this InsertFuturesOptionTickPriceDataCommand command, FuturesOptionTickPriceDataInsertion futuresOptionTickPriceDataInsertion)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickPriceDataInsertedEvent.Actor, FuturesOptionTickPriceDataInsertedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Contract = futuresOptionTickPriceDataInsertion.FuturesContract,
            TickData = futuresOptionTickPriceDataInsertion.FuturesOptionTickData,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
