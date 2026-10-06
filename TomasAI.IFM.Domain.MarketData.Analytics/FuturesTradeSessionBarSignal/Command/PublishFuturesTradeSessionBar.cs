using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command;

/// <summary>Applies one validated bar publication command to event-sourced publisher state.</summary>
public static class PublishFuturesTradeSessionBar
{
    /// <summary>Computes and validates the Futures Trade Session Bar Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this PublishFuturesTradeSessionBarCommand command, FuturesTradeSessionBarSignalCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        if (state.LastAppliedBar is { } lastBar && command.Bar.IntervalStartUtc == lastBar.IntervalStartUtc
            && command.Bar.IntervalEndUtc == lastBar.IntervalEndUtc)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply FuturesTradeSessionBarSignal event";
        var updated = command.Compute(out var futuresTradeSessionBar) switch
        {
            _ when futuresTradeSessionBar is null
                => command.UpdateFailed(ref errorMsg, "FuturesTradeSessionBarSignal payload is missing"),
            _ => state.Update(command.CreateFuturesTradeSessionBarPublishedEvent(futuresTradeSessionBar), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures Trade Session Bar Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresTradeSessionBar">The futures trade session bar business data used by this operation.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this PublishFuturesTradeSessionBarCommand command, out FuturesTradeSessionBarReadModel futuresTradeSessionBar)
    {
        futuresTradeSessionBar = command.Bar;
        return futuresTradeSessionBar is not null;
    }

    /// <summary>Creates the Futures Trade Session Bar Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresTradeSessionBar">The futures trade session bar business data used by this operation.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesTradeSessionBarPublishedEvent CreateFuturesTradeSessionBarPublishedEvent(this PublishFuturesTradeSessionBarCommand command, FuturesTradeSessionBarReadModel futuresTradeSessionBar) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesTradeSessionBarPublishedEvent.Actor, FuturesTradeSessionBarPublishedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        Bar = futuresTradeSessionBar
    };
}
