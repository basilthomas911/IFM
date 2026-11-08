using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

/// <summary>Seals verified old marks without rolling back the current position or inventing market observations.</summary>
public static class HistoricalPositionEndOfDay
{
    public static StrategyPositionSnapshot Seal(StrategyPositionSnapshot mark, StrategyPositionSnapshot current,
        DateOnly date, DateTime boundary)
    {
        ArgumentNullException.ThrowIfNull(mark);
        ArgumentNullException.ThrowIfNull(current);
        if (mark.Id != current.Id || mark.StrategyKind != TradeStrategyKind.IronCondor
            || mark.Phase != StrategyPositionPhase.MarkToMarket || !mark.IsOpen
            || mark.ValueDate != date || date >= current.ValueDate || mark.PositionSequence >= current.PositionSequence
            || boundary != FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime)
            throw new InvalidOperationException("POSITION.HISTORY.INVALID_SOURCE;A prior matching daily mark and exact session close are required.");
        var machine = new StrategyPositionActorStateMachine();
        machine.Replay(mark);
        var decision = machine.EndOfDay(date,boundary);
        if (!decision.Accepted) throw new InvalidOperationException($"{decision.Code};{decision.Detail}");
        return decision.Value!;
    }
}
