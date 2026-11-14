using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.Model;

internal static class PositionMarketUpdate
{
    internal static PositionChange Compute(StrategyPositionSnapshot? current, DateTime observedAtUtc,
        Func<StrategyPositionActorStateMachine, TradeDecision<StrategyPositionSnapshot>> update)
    {
        var machine = new StrategyPositionActorStateMachine();
        if (current is not null)
            machine.Replay(current);
        return Compute(machine, observedAtUtc, update);
    }

    internal static PositionChange Compute(StrategyPositionActorStateMachine machine, DateTime observedAtUtc,
        Func<StrategyPositionActorStateMachine, TradeDecision<StrategyPositionSnapshot>> update)
    {
        StrategyPositionSnapshot? caughtUp = null;
        if (machine.Current is { } current && observedAtUtc.Kind == DateTimeKind.Utc
            && FuturesTradingValueDate.TryGet(new DateTimeOffset(observedAtUtc), out var date)
            && PositionEndOfDayCatchUp.RequiredValueDate(current, date) is { } previousDate)
        {
            var boundary = FuturesTradingValueDate.GetSessionEndUtc(previousDate).UtcDateTime;
            var close = machine.EndOfDay(previousDate, boundary);
            if (!close.Accepted)
                return new(false, null, close.Code, close.Detail);
            caughtUp = close.Value;
        }

        var decision = update(machine);
        return new(decision.Accepted, decision.Value, decision.Code, decision.Detail)
        {
            CaughtUpEndOfDay = caughtUp
        };
    }

    internal static bool Apply<TEvent>(PositionChange change, Func<StrategyPositionSnapshot, TEvent> create,
        Func<TEvent, bool> apply, Func<TEvent, StrategyPositionSnapshot, TEvent> createEndOfDay)
        where TEvent : IEvent
    {
        // Apply the seal before the quote. Both facts are persisted under the
        // same command reservation and expected stream revision.
        var mark = create(change.PositionSnapshot!);
        return (change.CaughtUpEndOfDay is not { } seal || apply(createEndOfDay(mark, seal))) && apply(mark);
    }
}
