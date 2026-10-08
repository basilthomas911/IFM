using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Model;

internal static class VerticalSpreadPositionTransition
{
    /// <summary>Calculates a position transition without changing actor state or creating events.</summary>
    /// <param name="positionSnapshot">The current position snapshot.</param>
    /// <param name="transition">The strategy operation to calculate.</param>
    /// <returns>The proposed position snapshot or business rejection.</returns>
    internal static TradeDecision<StrategyPositionSnapshot> Compute(StrategyPositionSnapshot? positionSnapshot,
        Func<StrategyPositionActorStateMachine, TradeDecision<StrategyPositionSnapshot>> transition)
    {
        var machine = new StrategyPositionActorStateMachine();
        if (positionSnapshot is not null) machine.Replay(positionSnapshot);
        return transition(machine);
    }
}
