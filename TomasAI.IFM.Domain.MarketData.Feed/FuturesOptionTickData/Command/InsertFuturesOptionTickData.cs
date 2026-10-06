using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;

/// <summary>Handles InsertFuturesOptionTickData commands through computation and state-owned event application.</summary>
public static class InsertFuturesOptionTickData
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this InsertFuturesOptionTickDataCommand command, FuturesOptionTickDataCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesOptionTickDataInsertedEvent";
        var updated = command.Compute(out var futuresOptionTickDataInsertion) switch
        {
            _ => state.Update(command.CreateFuturesOptionTickDataInsertedEvent(futuresOptionTickDataInsertion), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="futuresOptionTickDataInsertion">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    internal static bool Compute(this InsertFuturesOptionTickDataCommand command, out FuturesOptionTickDataInsertion futuresOptionTickDataInsertion)
    {
        futuresOptionTickDataInsertion = new(command.Contract, command.OptionTickData);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="futuresOptionTickDataInsertion">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static FuturesOptionTickDataInsertedEvent CreateFuturesOptionTickDataInsertedEvent(this InsertFuturesOptionTickDataCommand command, FuturesOptionTickDataInsertion futuresOptionTickDataInsertion)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataInsertedEvent.Actor, FuturesOptionTickDataInsertedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Contract = futuresOptionTickDataInsertion.FuturesContract,
            TickData = futuresOptionTickDataInsertion.FuturesOptionTickData,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
