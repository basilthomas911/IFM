using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command;

/// <summary>Handles InsertFuturesEodData commands through computation and state-owned event application.</summary>
public static class InsertFuturesEodData
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    /// <exception cref="ArgumentNullException">Required tick, contract, or current EOD inputs are absent.</exception>
    public static ServiceResult<GuidResult> Execute(this InsertFuturesEodDataCommand command, FuturesEodDataCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesEodDataInsertedEvent";
        var updated = command.Compute(out var futuresEodData) switch
        {
            _ => state.Update(command.CreateFuturesEodDataInsertedEvent(futuresEodData), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="futuresEodData">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    /// <exception cref="ArgumentNullException">Required tick, contract, or current EOD inputs are absent.</exception>
    internal static bool Compute(this InsertFuturesEodDataCommand command, out FuturesEodDataV2ReadModel futuresEodData)
    {
        futuresEodData = FuturesEodDataModel.CreateFuturesEodData(
            command.ValueDate, command.FuturesTickData, command.Contract, command.EodDataToday,
            command.EodDataRange, command.NormCurveData, command.WindowSize, command.VixEodData);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="futuresEodData">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static FuturesEodDataInsertedEvent CreateFuturesEodDataInsertedEvent(this InsertFuturesEodDataCommand command, FuturesEodDataV2ReadModel futuresEodData)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesEodDataInsertedEvent.Actor, FuturesEodDataInsertedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            FuturesEodData = futuresEodData,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
