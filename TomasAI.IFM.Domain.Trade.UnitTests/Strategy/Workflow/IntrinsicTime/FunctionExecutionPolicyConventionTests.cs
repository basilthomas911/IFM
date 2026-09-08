using System.Collections;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

public sealed class FunctionExecutionPolicyConventionTests
{
    [Fact]
    public void Every_production_function_has_an_exact_frozen_policy_map_and_no_actor_deadline_or_observation_hooks()
    {
        var actors = typeof(TradeSelectionFunctionActor).Assembly.GetTypes()
            .Where(t => t.BaseType?.Name.StartsWith("BaseEventSourceFunctionActor`", StringComparison.Ordinal) == true).ToArray();
        actors.Should().BeEquivalentTo([typeof(RegimeDiscoveryFunctionActor), typeof(MarketConditionFunctionActor), typeof(TradeSelectionFunctionActor)]);
        foreach (var actor in actors)
        {
            var requestType = actor.BaseType!.GetGenericArguments()[1];
            var field = actor.GetField("_executionPolicyMap", BindingFlags.Static | BindingFlags.NonPublic)!;
            field.Should().NotBeNull(); field.IsInitOnly.Should().BeTrue();
            var map = field.GetValue(null)!; map.GetType().FullName.Should().Contain("Frozen");
            ((IEnumerable)map.GetType().GetProperty("Keys")!.GetValue(map)!).Cast<Type>().Should().Equal(requestType);
            actor.GetMethod("ResolveExecutionPolicy", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Should().NotBeNull();
            var methods = actor.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Select(m => m.Name);
            methods.Should().NotContain(["GetFunctionDeadline", "get_FunctionTimeProvider", "OnFunctionCommitted", "OnFunctionReplayed", "WithinDeadlineAsync"]);
        }
    }

    [Theory]
    [InlineData(FunctionFailureStage.Loading)]
    [InlineData(FunctionFailureStage.Execution)]
    [InlineData(FunctionFailureStage.Projection)]
    [InlineData(FunctionFailureStage.Persistence)]
    public async Task Mapped_policies_preserve_each_actors_existing_deadline_scope(FunctionFailureStage stage)
    {
        var selection = await TradeSelectionFixture.Command();
        var market = AssessmentFixture.Command();
        var regime = RegimeDiscoveryFunctionValidationTests.ValidCommand();
        var clock = new Clock(DateTime.UtcNow.AddDays(1));
        var sc = Substitute.For<ITradeSelectionFunctionContext>(); sc.TimeProvider.Returns(clock);
        sc.Logger.Returns(NullLogger<TradeSelectionFunctionActor>.Instance);
        var mc = Substitute.For<IMarketConditionFunctionContext>(); mc.TimeProvider.Returns(clock);
        mc.Logger.Returns(NullLogger<MarketConditionFunctionActor>.Instance);
        var rc = RegimeDiscoveryFunctionValidationTests.Context(); rc.TimeProvider.Returns(clock);
        var selectionPolicy = Resolve(new TradeSelectionFunctionActor(sc), selection, stage);
        var marketPolicy = Resolve(new MarketConditionFunctionActor(mc), market, stage);
        var regimePolicy = Resolve(new RegimeDiscoveryFunctionActor(rc), regime, stage);
        selectionPolicy.Clock.Should().BeSameAs(clock); marketPolicy.Clock.Should().BeSameAs(clock); regimePolicy.Clock.Should().BeSameAs(clock);
        selectionPolicy.DeadlineUtc.Should().Be(stage == FunctionFailureStage.Loading
            ? clock.Now.AddMilliseconds(TradeSelectionContracts.CommonPolicy(selection.SelectionBinding).MaximumExecutionMilliseconds) : selection.ExpiresAtUtc);
        marketPolicy.DeadlineUtc.Should().Be(stage == FunctionFailureStage.Loading
            ? clock.Now.AddMilliseconds(market.ParameterSet.MaximumExecutionMilliseconds) : market.ExpiresAtUtc);
        regimePolicy.DeadlineUtc.Should().Be(stage == FunctionFailureStage.Execution ? regime.ExpiresAtUtc : null);
    }

    [Theory]
    [InlineData(FunctionEventPhase.Committed)]
    [InlineData(FunctionEventPhase.Replayed)]
    public async Task Completion_observation_returns_the_same_event_for_all_three_actors(FunctionEventPhase phase)
    {
        var selection = await TradeSelectionFixture.Command();
        var completed = (await new TradeSelectionFunctionTests.FunctionFixture(selection).Execute()).Completed!;
        TradeSelectionFunctionActor.MapEvent(new(typeof(TradeSelectionFunctionCompletedEvent), selection, completed, Phase: phase), new Clock(selection.RequestedAtUtc))
            .Completed.Should().BeSameAs(completed);
        var market = AssessmentFixture.Command();
        var snapshot = MarketConditionAssessmentCalculationTests.Snapshot(market).Seal();
        var calculated = MarketConditionAssessmentCalculationTests.Calculate(market, snapshot);
        var marketEvent = MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentCompletedEvent), market,
            new MarketConditionExecutionCompleted(calculated, snapshot)), new Clock(market.RequestedAtUtc)).Completed!;
        MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentCompletedEvent), market, marketEvent, Phase: phase), new Clock(market.RequestedAtUtc))
            .Completed.Should().BeSameAs(marketEvent);
        var regime = RegimeDiscoveryFunctionValidationTests.ValidCommand(); var regimeEvent = new RegimeDiscoveryPipelineCompletedEvent();
        RegimeDiscoveryFunctionActor.MapEvent(new(typeof(RegimeDiscoveryPipelineCompletedEvent), regime, regimeEvent, Phase: phase), TimeProvider.System)
            .Completed.Should().BeSameAs(regimeEvent);
    }

    static FunctionExecutionPolicy Resolve(object actor, object command, FunctionFailureStage stage)
        => (FunctionExecutionPolicy)actor.GetType().GetMethod("ResolveExecutionPolicy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actor, [command, stage])!;
    sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
