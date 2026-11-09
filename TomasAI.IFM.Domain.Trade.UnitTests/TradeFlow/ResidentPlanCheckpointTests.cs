using FluentAssertions;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport.ResidentActors;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

/// <summary>Full committed plans survive serialization without sharing mutable request or leg state.</summary>
public sealed class ResidentPlanCheckpointTests
{
    [Theory]
    [InlineData(ResidentPilotKind.FuturesPlan)]
    [InlineData(ResidentPilotKind.VerticalSpreadPlan)]
    [InlineData(ResidentPilotKind.IronCondorPlan)]
    public void Committed_checkpoint_preserves_plan_version_and_isolates_every_request(ResidentPilotKind kind)
    {
        var inputs = new ResidentPilotInputs();
        var first = inputs.Create(kind, 1, 1);
        var next = inputs.Create(kind, 1, 2);
        switch (first)
        {
            case UpdateFuturesTradePlanCommand request:
                Verify(request, (UpdateFuturesTradePlanCommand)next, request.Position,
                    new FuturesTradePlanFunctionState(),
                    plan => new FuturesTradePlanUpdatedEvent { EventId = 9001, CommandId = request.CommandId,
                        Subject = request.Subject, Plan = plan, RequestFingerprint = request.Fingerprint() },
                    state => state.CaptureResidentCheckpoint(), state => state.PreviousPlan!);
                break;
            case UpdateVerticalSpreadTradePlanCommand request:
                Verify(request, (UpdateVerticalSpreadTradePlanCommand)next, request.Position,
                    new VerticalSpreadTradePlanFunctionState(),
                    plan => new VerticalSpreadTradePlanUpdatedEvent { EventId = 9001, CommandId = request.CommandId,
                        Subject = request.Subject, Plan = plan, RequestFingerprint = request.Fingerprint() },
                    state => state.CaptureResidentCheckpoint(), state => state.PreviousPlan!);
                break;
            case UpdateIronCondorTradePlanCommand request:
                Verify(request, (UpdateIronCondorTradePlanCommand)next, request.Position,
                    new IronCondorTradePlanFunctionState(),
                    plan => new IronCondorTradePlanUpdatedEvent { EventId = 9001, CommandId = request.CommandId,
                        Subject = request.Subject, Plan = plan, RequestFingerprint = request.Fingerprint() },
                    state => state.CaptureResidentCheckpoint(), state => state.LatestSourceTradePlan!);
                break;
        }
    }

    static void Verify<TRequest, TState, TEvent>(TRequest first, TRequest next, StrategyPositionSnapshot position,
        TState state, Func<StrategyTradePlanSnapshot, TEvent> createEvent,
        Func<TState, ResidentFunctionCheckpoint<TRequest, TState>?> capture, Func<TState, StrategyTradePlanSnapshot> planOf)
        where TRequest : class, ICommand
        where TState : BaseEventSourceActorState<TState>, IEventSourceFunctionState<TState, TRequest, TEvent>, new()
        where TEvent : class, ICompleteEvent
    {
        Assert.Null(capture(state));
        var plan = new StrategyTradePlanSnapshot { Position = position, PlanRevision = 17,
            ValueDate = position.ValueDate, OpeningValue = 100, CurrentValue = 123, TotalPnl = 23,
            Parameters = new TradePlanParameters(), ReasonCode = "CHECKPOINT.TEST", ContentHash = "stable",
            IronCondorTradePlanSnapshot = position.StrategyKind == TradeStrategyKind.IronCondor
                ? new IronCondorTradePlanSnapshot { Position = position, SequenceId = 17, MaxLoss = 400,
                    MinProfitTarget = 120, DailyProfitTarget = 80, TradePnl = 23, CalculationStatus = "Qualified" } : null };
        state.ReplayEvents(new IEvent[] { createEvent(plan) });
        state.RestoreCommittedStreamVersion(7);
        var checkpoint = Assert.IsType<ResidentFunctionCheckpoint<TRequest, TState>>(capture(state));
        Assert.True(checkpoint.RetainedBytes > 0);
        var replay = checkpoint.Restore(first);
        var later = checkpoint.Restore(next);
        Assert.True(replay.IsCompleted);
        Assert.True(replay.Matches(first));
        Assert.False(later.IsCompleted);
        Assert.Equal(7, later.CommittedStreamVersion); // stream version is not global EventId 9001
        planOf(later).Should().BeEquivalentTo(plan, options => options.ComparingByMembers<StrategyTradePlanSnapshot>().ComparingByMembers<StrategyPositionSnapshot>());
        var originalPrice = position.Legs[0].CurrentPrice;
        position.Legs[0] = position.Legs[0] with { CurrentPrice = -999 };
        planOf(replay).Position.Legs[0] = planOf(replay).Position.Legs[0] with { CurrentPrice = -888 };
        Assert.Equal(originalPrice, planOf(later).Position.Legs[0].CurrentPrice);
        Assert.Equal(originalPrice, planOf(checkpoint.Restore(next)).Position.Legs[0].CurrentPrice);
        Assert.Empty(later.Events);
    }
}
