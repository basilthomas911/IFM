using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class DailyExitIntentRecoveryTests
{
    static string DirectoryForTest() => Path.Combine(Path.GetTempPath(), "ifm-daily-exit-tests", Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("Position")] [InlineData("Clock")]
    public async Task Cold_recovery_reuses_first_intent_before_a_later_plan_can_be_generated(string kind)
    {
        var path = DirectoryForTest(); var first = FuturesPositionDeliveryFixture.DailyInput(kind, 1); byte[] command;
        await using (var original = new FuturesPositionDeliveryFixture(false, intentDirectory: path) { Failure = "exit-ambiguous" })
        {
            await original.Start(); Assert.False(await original.Deliver(first));
            command = MessagePackSerializer.Serialize(original.ExitAttempts.Single());
        }
        await using var next = new FuturesPositionDeliveryFixture(false, intentDirectory: path) { RecoveryPosition = first.PositionSnapshot };
        next.Time.Now += TimeSpan.FromDays(1); await next.Start();
        Assert.Equal(command, MessagePackSerializer.Serialize(Assert.Single(next.ExitAttempts)));
        Assert.Empty(next.Intents.Pending());
        Assert.True(await next.Deliver(FuturesPositionDeliveryFixture.DailyInput(kind, 2)));
        Assert.Empty(next.Requests); Assert.Single(next.ExitAttempts);
    }
    [Theory]
    [InlineData("closed")] [InlineData("new-generation")]
    public async Task Recovery_never_sends_an_exit_to_closed_or_new_generation_positions(string state)
    {
        var path = DirectoryForTest(); var first = FuturesPositionDeliveryFixture.DailyInput("Clock", 1);
        await using (var original = new FuturesPositionDeliveryFixture(false, intentDirectory: path) { Failure = "exit-send" })
        { await original.Start(); Assert.False(await original.Deliver(first)); }
        await using var next = new FuturesPositionDeliveryFixture(false, intentDirectory: path)
        { RecoveryPosition = first.PositionSnapshot with { IsOpen = state != "closed", RouteGeneration = state == "new-generation" ? 8 : 7 } };
        await next.Start(); Assert.Empty(next.ExitAttempts); Assert.Empty(next.Intents.Pending());
        Assert.Equal(DailyExitIntentStage.Retired, next.Intents.Find(first.PositionSnapshot)!.Stage);
    }
    [Theory]
    [InlineData("missing")] [InlineData("behind")] [InlineData("entry-conflict")]
    public async Task Unverifiable_recovery_position_keeps_the_intent_pending(string state)
    {
        var path = DirectoryForTest(); var first = FuturesPositionDeliveryFixture.DailyInput("Clock", 1);
        await using (var original = new FuturesPositionDeliveryFixture(false, intentDirectory: path) { Failure = "exit-send" })
        { await original.Start(); Assert.False(await original.Deliver(first)); }
        await using var next = new FuturesPositionDeliveryFixture(false, intentDirectory: path)
        { RecoveryPosition = state == "missing" ? null : first.PositionSnapshot with { RouteGeneration = state == "behind" ? 6 : 7,
            DailyFuturesEntry = first.PositionSnapshot.DailyFuturesEntry! with { DcTriggerId = state == "entry-conflict" ? Guid.NewGuid() : first.PositionSnapshot.DailyFuturesEntry.DcTriggerId } } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => next.Start().AsTask());
        Assert.Single(next.Intents.Pending()); Assert.Empty(next.ExitAttempts);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Unsuccessful_or_wrong_owner_reply_does_not_confirm_intent(bool wrongId)
    {
        await using var fixture = new FuturesPositionDeliveryFixture(false) { RejectOwner = !wrongId, WrongOwnerReply = wrongId }; await fixture.Start();
        var source = FuturesPositionDeliveryFixture.DailyInput("Clock", 1);
        Assert.False(await fixture.Deliver(source)); Assert.Single(fixture.Intents.Pending());
        fixture.RejectOwner = fixture.WrongOwnerReply = false;
        Assert.True(await fixture.Deliver(FuturesPositionDeliveryFixture.DailyInput("Position", 2)));
        Assert.Empty(fixture.Intents.Pending()); Assert.Single(fixture.Requests);
    }
    [Fact]
    public async Task Failed_capture_write_prevents_exit_dispatch()
    {
        var path = DirectoryForTest(); await using var fixture = new FuturesPositionDeliveryFixture(false, intentDirectory: path); await fixture.Start();
        var source = FuturesPositionDeliveryFixture.DailyInput("Clock", 1);
        var temporary = Path.Combine(path, "pending", DailyFuturesExitIntents.Key(source.PositionSnapshot).ToString("N") + ".dfe.tmp");
        Directory.CreateDirectory(temporary);
        Assert.False(await fixture.Deliver(source)); Assert.Empty(fixture.ExitAttempts);
        Directory.Delete(temporary); Assert.True(await fixture.Deliver(source)); Assert.Single(fixture.ExitAttempts);
    }
    [Fact]
    public async Task Confirmed_intent_keeps_the_same_authority_when_replayed_after_midnight()
    {
        await using var fixture = new FuturesPositionDeliveryFixture(false); await fixture.Start();
        var first = FuturesPositionDeliveryFixture.DailyInput("Clock", 1); Assert.True(await fixture.Deliver(first));
        var next = FuturesPositionDeliveryFixture.DailyInput("Position", 2);
        Assert.True(await fixture.Deliver(next with { PositionSnapshot = next.PositionSnapshot with { AsOfUtc = next.PositionSnapshot.AsOfUtc.AddDays(1) } }));
        Assert.Single(fixture.Requests); Assert.Single(fixture.ExitAttempts);
    }
    [Fact]
    public async Task Corrupted_pending_intent_blocks_restart()
    {
        var path = DirectoryForTest();
        await using (var fixture = new FuturesPositionDeliveryFixture(false, intentDirectory: path) { Failure = "exit-send" })
        { await fixture.Start(); Assert.False(await fixture.Deliver(FuturesPositionDeliveryFixture.DailyInput("Clock", 1))); }
        var file = Directory.GetFiles(Path.Combine(path, "pending"), "*.dfe").Single(); var bytes = File.ReadAllBytes(file); bytes[^1] ^= 1; File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidDataException>(() => new DailyFuturesExitIntents(path));
    }
}
