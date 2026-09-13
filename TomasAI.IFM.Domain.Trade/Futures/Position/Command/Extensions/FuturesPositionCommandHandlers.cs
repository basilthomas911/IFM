using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.Extensions;

public static class FuturesPositionCommandHandlers
{
    public static ServiceResult<GuidResult> Execute(
        this OpenFuturesPositionCommand command,
        FuturesPositionCommandState state) =>
        Apply(command, state, machine => machine.Open(command.Trade, command.EntityId.PositionId, command.EffectiveAtUtc));

    public static ServiceResult<GuidResult> Execute(
        this UpdateFuturesPositionMarketPriceCommand command,
        FuturesPositionCommandState state) =>
        Apply(command, state, machine => machine.UpdateLeg(
            command.TradeLegId, command.Price, command.SourceSequence,
            command.EffectiveAtUtc, command.RouteGeneration));

    public static ServiceResult<GuidResult> Execute(
        this EndOfDayFuturesPositionCommand command,
        FuturesPositionCommandState state) =>
        Apply(command, state, machine => machine.EndOfDay(command.EffectiveAtUtc));

    public static ServiceResult<GuidResult> Execute(
        this CloseFuturesPositionCommand command,
        FuturesPositionCommandState state) =>
        Apply(command, state, machine => machine.Close(command.EffectiveAtUtc));

    public static ServiceResult<GuidResult> Execute(
        this CorrectFuturesPositionBasisCommand command,
        FuturesPositionCommandState state) =>
        Apply(command, state, machine => machine.CorrectBasis(
            command.TradeLegId, command.Price, command.EffectiveAtUtc));

    public static ServiceResult<GuidResult> Execute(
        this SnapshotFuturesPositionCommand command,
        FuturesPositionCommandState state) =>
        state.Current is not null
            ? TradeCommandResult.Accepted(command.CommandId)
            : new ServiceFailed<GuidResult>(command.ErrorCode, "POSITION.NOT_FOUND");

    static ServiceResult<GuidResult> Apply(
        FuturesPositionCommand command,
        FuturesPositionCommandState state,
        Func<StrategyPositionActorStateMachine, TradeDecision<StrategyPositionSnapshot>> transition)
    {
        var machine = new StrategyPositionActorStateMachine();
        if (state.Current is { } current) machine.Replay(current);
        var decision = transition(machine);
        if (!decision.Accepted || decision.Value is null)
            return TradeCommandResult.Rejected(command.ErrorCode, decision);
        state.Update(new FuturesPositionChangedEvent
        {
            EntityId = command.EntityId,
            State = decision.Value
        }, command);
        return TradeCommandResult.Accepted(command.CommandId);
    }
}
