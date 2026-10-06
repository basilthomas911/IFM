using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

/// <summary>Handles TurnTradeLiveFeedOn commands through computation and state-owned event application.</summary>
public static class TurnTradeLiveFeedOn
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this TurnTradeLiveFeedOnCommand command, MarketDataFeedCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply TradeLiveFeedTurnedOnEvent";
        var updated = command.Compute(state.IsTradeLiveFeedOn, out var tradeLiveFeedActivation) switch
        {
            _ when !tradeLiveFeedActivation.Accepted
                => command.UpdateFailed(ref errorMsg, $"Trade live feed is already on for: {command.OrderId}:{command.TradeId}"),
            _ => state.Update(command.CreateTradeLiveFeedTurnedOnEvent(tradeLiveFeedActivation), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="isTradeLiveFeedOn">Whether the owning state currently has an active trade feed.</param>
    /// <param name="tradeLiveFeedActivation">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    internal static bool Compute(this TurnTradeLiveFeedOnCommand command, bool isTradeLiveFeedOn, out TradeLiveFeedActivation tradeLiveFeedActivation)
    {
        tradeLiveFeedActivation = new(command.OrderId, command.TradeId, !isTradeLiveFeedOn);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="tradeLiveFeedActivation">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static TradeLiveFeedTurnedOnEvent CreateTradeLiveFeedTurnedOnEvent(this TurnTradeLiveFeedOnCommand command, TradeLiveFeedActivation tradeLiveFeedActivation)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, TradeLiveFeedTurnedOnEvent.Actor, TradeLiveFeedTurnedOnEvent.Verb, command.EntityId.Format()),
            EntityId = new TradeLiveFeedId(command.OrderId, command.TradeId, command.ValueDate),
            OrderId = tradeLiveFeedActivation.OrderId,
            TradeId = tradeLiveFeedActivation.TradeId,
            UpdatedOn = command.OriginatedOn,
            UpdatedBy = command.OriginatedBy
        };
}
