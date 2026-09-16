using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class MicroExecutionModelTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Opening_places_once_then_moves_one_tick_only_when_quote_and_cadence_are_valid()
    {
        var policy = Policy();
        var input = Input() with { BrokerAcknowledged = false };
        policy.Decide(input).Should().BeEquivalentTo(new MicroExecutionDecision(
            MicroExecutionAction.Place,
            MicroExecutionActionMask.Wait | MicroExecutionActionMask.Place,
            10m,
            "InitialPlace"));

        var update = policy.Decide(Input());
        update.Action.Should().Be(MicroExecutionAction.UpdateLimit);
        update.NewLimit.Should().Be(10.25m);

        policy.Decide(Input() with { QuoteAtUtc = Now.AddSeconds(-3) }).Action
            .Should().Be(MicroExecutionAction.Wait);
        policy.Decide(Input() with { LastMutationAtUtc = Now.AddMilliseconds(-100) }).Action
            .Should().Be(MicroExecutionAction.Wait);
    }

    [Fact]
    public void Unknown_outcome_reconciles_and_closed_gate_cancels_only_a_known_opening_order()
    {
        var policy = Policy();
        policy.Decide(Input() with { OutcomeUnknown = true }).Action
            .Should().Be(MicroExecutionAction.Reconcile);
        policy.Decide(Input() with { GateAllowsNewRisk = false }).Action
            .Should().Be(MicroExecutionAction.Cancel);
        policy.Decide(Input() with { GateAllowsNewRisk = false, BrokerAcknowledged = false }).Action
            .Should().Be(MicroExecutionAction.Wait);
    }

    [Fact]
    public void Closing_profile_can_cross_to_ask_but_never_escape_the_approved_limit()
    {
        var input = Input() with
        {
            Opening = false,
            Profile = Input().Profile with { UrgentClosing = true },
            Ask = 10.75m,
            MaximumLimit = 10.50m
        };
        var decision = Policy().Decide(input);
        decision.Action.Should().Be(MicroExecutionAction.UpdateLimit);
        decision.NewLimit.Should().Be(10.50m);
    }

    private static MicroExecutionPolicy Policy() => new(
        new MicroExecutionConstraintEvaluator(), new MicroExecutionPriceCalculator());

    private static MicroExecutionInput Input() => new(
        new("default", 1, "hash", TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(2), 3, false),
        true, true, true, false, false, 10m, 9m, 11m, 0.25m,
        10m, 10.50m, Now, Now, Now.AddSeconds(5), Now.AddSeconds(-1), 0);
}
