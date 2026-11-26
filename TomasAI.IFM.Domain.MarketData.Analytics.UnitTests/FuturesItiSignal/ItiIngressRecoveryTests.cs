using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

[CollectionDefinition("ItiIngress", DisableParallelization = true)]
public sealed class ItiIngressCollection { }
[Collection("ItiIngress")]
public sealed class ItiIngressRecoveryTests
{
    static string Root() => Path.Combine(Path.GetTempPath(), "IFM-iti-ingress-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("refused")]
    [InlineData("throw")]
    [InlineData("lost-reply")]
    [InlineData("null")]
    public async Task Failed_command_is_not_ACKed_and_cold_recovery_keeps_original_inputs(string mode)
    {
        var root = Root(); var effects = new ItiDeliveryFixture.Effects
        { Fail = mode == "refused", Throw = mode == "throw", ThrowAfterCommit = mode == "lost-reply", NullReply = mode == "null" };
        using (var store = new FuturesItiIngressRecovery(root))
        {
            await using var first = new ItiDeliveryFixture(store, effects); await first.Start();
            Assert.False(await first.Deliver(ItiDeliveryFixture.Source(1)));
            Assert.Equal(1, store.PendingCount);
        }
        var original = effects.Attempts.Single();
        effects.Fail = effects.Throw = effects.ThrowAfterCommit = effects.NullReply = false;
        using var restored = new FuturesItiIngressRecovery(root);
        await using var second = new ItiDeliveryFixture(restored, effects) { Vx = 99 };
        ItiDeliveryFixture.SeedAtr(99);
        await second.Start(); // Recovery must work before another market event, using no new market inputs.
        Assert.Equal(0, restored.PendingCount);
        var retry = effects.Attempts.Last();
        Assert.Equal(original.CommandId, retry.CommandId); Assert.Equal(20, retry.VixFuturesPrice);
        Assert.Equal(8, retry.DailyAtr!.Value); Assert.Equal(original.Timestamp, retry.Timestamp);
        Assert.Equal(original.DailyAtr, retry.DailyAtr);
        Assert.Equal(TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(original), TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(retry));
        Assert.Single(effects.Committed); Assert.Equal(1, effects.Changes);
        Assert.True(await second.Deliver(ItiDeliveryFixture.Source(1)));
        Assert.Equal(2, effects.Attempts.Count); // Completed redelivery performs no second request.
    }
    [Fact]
    public async Task Later_input_is_captured_but_cannot_overtake_failed_earlier_command()
    {
        using var store = new FuturesItiIngressRecovery(Root());
        var effects = new ItiDeliveryFixture.Effects { Fail = true };
        await using var fixture = new ItiDeliveryFixture(store, effects);
        Assert.False(await fixture.Receive(ItiDeliveryFixture.Source(1)));
        fixture.Vx = 30; ItiDeliveryFixture.SeedAtr(12);
        Assert.False(await fixture.Receive(ItiDeliveryFixture.Source(2, 105)));
        Assert.Equal(new[] { ItiDeliveryFixture.Source(1).Id, ItiDeliveryFixture.Source(1).Id }, effects.Attempts.Select(c => c.CommandId));
        Assert.Equal(2, store.PendingCount);
        effects.Fail = false; fixture.Vx = 99; ItiDeliveryFixture.SeedAtr(99);
        Assert.True(await store.RecoverAsync((command, token) => FuturesMarketPriceUpdated.DispatchAsync(fixture.Context, command, token)));
        Assert.Equal(new double[] { 20, 30 }, effects.Committed.Select(c => c.VixFuturesPrice));
        Assert.Equal(new double[] { 8, 12 }, effects.Committed.Select(c => c.DailyAtr!.Value));
        Assert.Equal(new[] { ItiDeliveryFixture.Source(1).Id, ItiDeliveryFixture.Source(2).Id }, effects.Committed.Select(c => c.CommandId));
    }
    [Fact]
    public async Task Conflicting_reused_identity_is_rejected_without_a_second_request()
    {
        using var store = new FuturesItiIngressRecovery(Root()); await using var fixture = new ItiDeliveryFixture(store);
        Assert.True(await fixture.Receive(ItiDeliveryFixture.Source(1)));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Receive(ItiDeliveryFixture.Source(1, 101)).AsTask());
        Assert.Single(fixture.Service.Attempts);
    }
    [Fact]
    public async Task Compaction_and_bounded_completed_window_survive_reopen()
    {
        var root = Root(); var effects = new ItiDeliveryFixture.Effects();
        using (var store = new FuturesItiIngressRecovery(root, completedLimit: 2, compactBytes: 1024))
        {
            await using var fixture = new ItiDeliveryFixture(store, effects);
            for (var n = 1; n <= 10; n++) Assert.True(await fixture.Receive(ItiDeliveryFixture.Source(n)));
        }
        using var restored = new FuturesItiIngressRecovery(root, completedLimit: 2, compactBytes: 1024);
        await using var second = new ItiDeliveryFixture(restored, effects);
        Assert.True(await second.Receive(ItiDeliveryFixture.Source(10)));
        await Assert.ThrowsAsync<InvalidDataException>(() => second.Receive(ItiDeliveryFixture.Source(1)).AsTask());
        Assert.Equal(10, effects.Committed.Count);
    }
    [Fact]
    public async Task Partial_tail_is_recovered_but_a_complete_corrupt_frame_fails_closed()
    {
        var root = Root();
        using (var store = new FuturesItiIngressRecovery(root))
        {
            await using var fixture = new ItiDeliveryFixture(store, new() { Fail = true });
            Assert.False(await fixture.Receive(ItiDeliveryFixture.Source(1)));
        }
        using (var file = new FileStream(Path.Combine(root, "ingress.fig"), FileMode.Append)) file.Write(new byte[13]);
        using (var store = new FuturesItiIngressRecovery(root)) Assert.Equal(1, store.PendingCount);
        var bytes = File.ReadAllBytes(Path.Combine(root, "ingress.fig")); bytes[^1] ^= 1;
        File.WriteAllBytes(Path.Combine(root, "ingress.fig"), bytes);
        Assert.Throws<InvalidDataException>(() => new FuturesItiIngressRecovery(root));
    }
    [Fact]
    public async Task Source_ordinals_allow_new_trades_at_repeated_or_backwards_exchange_timestamps()
    {
        using var store = new FuturesItiIngressRecovery(Root(), completedLimit: 2);
        await using var fixture = new ItiDeliveryFixture(store);
        var one = ItiDeliveryFixture.Source(1);
        var two = ItiDeliveryFixture.Source(2);
        two = two with { Price = two.Price with { Trade = two.Price.Trade!.Value with { EventTimestamp = one.Price.Trade!.Value.EventTimestamp } } };
        Assert.True(await fixture.Receive(one)); Assert.True(await fixture.Receive(two));
        var three = ItiDeliveryFixture.Source(3);
        three = three with { Price = three.Price with { Trade = three.Price.Trade!.Value with { EventTimestamp = one.Price.Trade!.Value.EventTimestamp } } };
        Assert.True(await fixture.Receive(three));
        var four = ItiDeliveryFixture.Source(4);
        four = four with { Price = four.Price with { Trade = four.Price.Trade!.Value with { EventTimestamp = one.Price.Trade!.Value.EventTimestamp.AddTicks(-1) } } };
        Assert.True(await fixture.Receive(four));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Receive(one).AsTask());
        Assert.Equal(4, fixture.Service.Attempts.Count);
    }
    [Fact]
    public async Task Command_actor_preserves_frozen_ATR_instead_of_sampling_the_new_cache_value()
    {
        using var store = new FuturesItiIngressRecovery(); await using var fixture = new ItiDeliveryFixture(store);
        Assert.True(await fixture.Receive(ItiDeliveryFixture.Source(1)));
        var command = fixture.Service.Committed.Single(); ItiDeliveryFixture.SeedAtr(25);
        var context = NSubstitute.Substitute.For<TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor.IFuturesItiSignalCommandContext>();
        NSubstitute.SubstituteExtensions.Returns(context.Logger, Microsoft.Extensions.Logging.Abstractions.NullLogger<TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor.FuturesItiSignalCommandActor>.Instance);
        NSubstitute.SubstituteExtensions.Returns(context.ActorId, new TomasAI.IFM.Shared.EventModelActor.ActorMailboxId(TomasAI.IFM.Shared.EventModelActor.ActorType.Command, "FuturesItiSignalCommand"));
        var actor = new FrozenCommandActor(context);
        var state = new TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiSignalCommandState();
        Assert.True((await actor.Run(state, command)).Success);
        Assert.Equal(8, state.FuturesItiSignal!.Threshold);
    }
    sealed class FrozenCommandActor(TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor.IFuturesItiSignalCommandContext context)
        : TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor.FuturesItiSignalCommandActor(context)
    {
        public ValueTask<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TomasAI.IFM.Shared.EventSourcing.GuidResult>> Run(
            TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiSignalCommandState state,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands.GenerateFuturesItiSignalCommand command)
            => ReceiveAsync(context, state, command);
    }
    [Fact]
    public async Task Changing_intake_modes_cannot_strand_required_fallback_work()
    {
        using var store = new FuturesItiIngressRecovery();
        await using var fixture = new ItiDeliveryFixture(store, new() { Fail = true });
        Assert.False(await fixture.Receive(ItiDeliveryFixture.Source(1)));
        var actor = new OrderedStartupActor(fixture.Context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => actor.TryStart().AsTask());
        Assert.Equal(1, store.PendingCount); Assert.Single(fixture.Service.Attempts);
    }
    sealed class OrderedStartupActor(TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor.IFuturesItiSignalRealtimeContext context)
        : TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor.FuturesItiSignalRealtimeActor(context,
            new TomasAI.IFM.Shared.EventModelActor.ResidentActorPilotOptions { FuturesItiOrderedIntakeEnabled = true })
    {
        public ValueTask TryStart() => OnStartup(context);
    }
    [Fact]
    public void Recovery_directory_has_one_exclusive_owner()
    {
        var root = Root(); using var store = new FuturesItiIngressRecovery(root);
        Assert.Throws<IOException>(() => new FuturesItiIngressRecovery(root));
    }
}
