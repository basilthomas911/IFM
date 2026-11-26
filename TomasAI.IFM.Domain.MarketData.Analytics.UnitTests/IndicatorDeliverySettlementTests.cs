using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.RealtimeDelivery;

[CollectionDefinition("IndicatorDelivery", DisableParallelization = true)]
public sealed class IndicatorDeliveryCollection { }

[Collection("IndicatorDelivery")]
public sealed class IndicatorDeliverySettlementTests
{
    public static IEnumerable<object[]> FailureCases => IndicatorDeliveryFixture.Kinds
        .SelectMany(kind => new[] { "refused", "throw", "lost-reply" }.Select(mode => new object[] { kind, mode }));
    public static IEnumerable<object[]> Actors => IndicatorDeliveryFixture.Kinds.Select(kind => new object[] { kind });

    [Theory, MemberData(nameof(FailureCases))]
    public async Task Failure_is_not_ACKed_and_ordered_redelivery_survives_actor_restart(string kind, string mode)
    {
        var state = new IndicatorDeliveryFixture.State
        { Fail = mode == "refused", Throw = mode == "throw", ThrowAfterCommit = mode == "lost-reply" };
        await using (var first = new IndicatorDeliveryFixture(kind, state))
        {
            await first.Start();
            var message = first.Input(1);
            Assert.False(await first.Deliver(message));
            Assert.Equal(1, message.Settlements); Assert.Equal(1, message.Disposals);
            Assert.Equal(new long[] { 1 }, state.Attempts);
        }
        state.Fail = state.Throw = state.ThrowAfterCommit = false;
        await using var restarted = new IndicatorDeliveryFixture(kind, state);
        await restarted.Start();
        Assert.True(await restarted.Deliver(restarted.Input(1)));
        Assert.True(await restarted.Deliver(restarted.Input(2)));
        Assert.Equal(new long[] { 1, 1, 2 }, state.Attempts);
        Assert.Equal(new long[] { 1, 2 }, state.Committed);
        // The driver owns retry ordering. This test does not make the old local queue durable.
    }

    [Theory, MemberData(nameof(FailureCases))]
    public async Task Same_actor_can_retry_the_failed_input_before_the_next_input(string kind, string mode)
    {
        var state = new IndicatorDeliveryFixture.State
        { Fail = mode == "refused", Throw = mode == "throw", ThrowAfterCommit = mode == "lost-reply" };
        await using var runtime = new IndicatorDeliveryFixture(kind, state); await runtime.Start();
        Assert.False(await runtime.Deliver(runtime.Input(1)));
        state.Fail = state.Throw = state.ThrowAfterCommit = false;
        Assert.True(await runtime.Deliver(runtime.Input(1)));
        Assert.True(await runtime.Deliver(runtime.Input(2)));
        Assert.Equal(new long[] { 1, 1, 2 }, state.Attempts);
        Assert.Equal(new long[] { 1, 2 }, state.Committed);
    }

    [Theory, MemberData(nameof(Actors))]
    public async Task Inapplicable_source_still_completes_successfully_without_a_command(string kind)
    {
        await using var runtime = new IndicatorDeliveryFixture(kind);
        await runtime.Start();
        Assert.True(await runtime.Deliver(runtime.Input(1, applicable: false)));
        Assert.Empty(runtime.Effects.Attempts);
    }

    [Theory]
    [InlineData("Vwap")]
    [InlineData("VxTermStructure")]
    public async Task Missing_required_catalogue_is_not_ACKed(string kind)
    {
        await using var runtime = new IndicatorDeliveryFixture(kind) { CatalogAvailable = false };
        await runtime.Start();
        Assert.False(await runtime.Deliver(runtime.Input(1)));
        Assert.Empty(runtime.Effects.Attempts);
    }
}
