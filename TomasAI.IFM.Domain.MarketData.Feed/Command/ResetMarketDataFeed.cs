using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

/// <summary>Handles ResetMarketDataFeed commands through computation and state-owned event application.</summary>
public static class ResetMarketDataFeed
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ResetMarketDataFeedCommand command, MarketDataFeedCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply MarketDataFeedResetEvent";
        var updated = command.Compute(out var marketDataFeedReset) switch
        {
            _ => state.Update(command.CreateMarketDataFeedResetEvent(marketDataFeedReset), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="marketDataFeedReset">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    internal static bool Compute(this ResetMarketDataFeedCommand command, out MarketDataFeedReset marketDataFeedReset)
    {
        marketDataFeedReset = new(command.FuturesContracts, command.ValueDate);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="marketDataFeedReset">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static MarketDataFeedResetEvent CreateMarketDataFeedResetEvent(this ResetMarketDataFeedCommand command, MarketDataFeedReset marketDataFeedReset)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedResetEvent.Actor, MarketDataFeedResetEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            FuturesContracts = marketDataFeedReset.FuturesContracts,
            ValueDate = marketDataFeedReset.ValueDate,
            ResetOn = command.OriginatedOn,
            ResetBy = command.OriginatedBy
        };
}
