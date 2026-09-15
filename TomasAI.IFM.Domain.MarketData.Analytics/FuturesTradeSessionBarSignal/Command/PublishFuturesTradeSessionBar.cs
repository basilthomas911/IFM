using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command;

/// <summary>Applies one validated bar publication command to event-sourced publisher state.</summary>
public static class PublishFuturesTradeSessionBar
{
    /// <summary>Acknowledges a proven interval repeat or publishes any other valid completed bar.</summary>
    public static ServiceResult<GuidResult> Execute(
        this PublishFuturesTradeSessionBarCommand command,
        FuturesTradeSessionBarSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        var bar = command.Bar;
        if (state.LastAppliedBar is { } last
            && bar.IntervalStartUtc == last.IntervalStartUtc
            && bar.IntervalEndUtc == last.IntervalEndUtc)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));

        return state.Update(command.CreatePublishedEvent(), command)
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed("BAR.UPDATE_FAILED;Unable to apply the completed-bar Published event.");
    }

    static FuturesTradeSessionBarPublishedEvent CreatePublishedEvent(
        this PublishFuturesTradeSessionBarCommand command) => new()
    {
        Subject = new(ActorType.Event, FuturesTradeSessionBarPublishedEvent.Actor,
            FuturesTradeSessionBarPublishedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        Bar = command.Bar
    };

}
