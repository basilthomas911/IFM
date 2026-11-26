using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class IronCondorPlanPublicationTests
{
    static string DirectoryForTest() => Path.Combine(Path.GetTempPath(), "ifm-condor-publication-tests", Guid.NewGuid().ToString("N"));
    static StrategyTradePlanSnapshot Plan(int sequence) => new()
    { Position = IronCondorPositionDeliveryFixture.Position(sequence), ValueDate = new(2026, 8, 25), PlanRevision = sequence };
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Retry_keeps_exact_event_identity_timestamp_and_payload_despite_unchanged_cache(bool ambiguous)
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false, realPublication: true);
        fixture.FailBeforePublication = !ambiguous; fixture.FailAfterPublication = ambiguous;
        var plan = Plan(1);
        await Assert.ThrowsAsync<IOException>(() => fixture.PublicationContext!.PublishRealtimeAsync(plan.Position, plan).AsTask());
        var original = fixture.Requests.Publications.Pending().Single().Event;
        var origin = TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(original);
        Assert.NotNull(origin);
        fixture.Time.Now += TimeSpan.FromHours(1); fixture.FailBeforePublication = fixture.FailAfterPublication = false;
        await fixture.PublicationContext!.PublishRealtimeAsync(plan.Position, plan);
        Assert.Empty(fixture.Requests.Publications.Pending());
        Assert.All(fixture.Notifications, sent => Assert.Equal(MessagePackSerializer.Serialize(original), MessagePackSerializer.Serialize(sent)));
        Assert.All(fixture.Notifications, sent => Assert.Equal(origin, TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(sent)));
        Assert.Equal(ambiguous ? 2 : 1, fixture.Notifications.Count);
        await fixture.PublicationContext.PublishRealtimeAsync(plan.Position, plan);
        Assert.Equal(ambiguous ? 2 : 1, fixture.Notifications.Count);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Startup_drains_original_notification_before_new_revision(bool ambiguous)
    {
        var path = DirectoryForTest(); byte[] original;
        await using (var first = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path, realPublication: true))
        {
            first.FailBeforePublication = !ambiguous; first.FailAfterPublication = ambiguous;
            var plan = Plan(1);
            await Assert.ThrowsAsync<IOException>(() => first.PublicationContext!.PublishRealtimeAsync(plan.Position, plan).AsTask());
            original = MessagePackSerializer.Serialize(first.Requests.Publications.Pending().Single().Event);
        }
        await using var next = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path, realPublication: true);
        await next.Start(); Assert.Single(next.Notifications);
        Assert.Equal(original, MessagePackSerializer.Serialize(next.Notifications[0]));
        var second = Plan(2); await next.PublicationContext!.PublishRealtimeAsync(second.Position, second);
        Assert.Equal(new long[] { 1, 2 }, next.Notifications.Select(value => value.Snapshot.Position!.PositionSequence));
        Assert.Empty(next.Requests.Publications.Pending());
    }
    [Fact]
    public async Task Unsettled_older_send_prevents_later_revision_from_overtaking()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false, realPublication: true) { FailBeforePublication = true };
        var first = Plan(1); var second = Plan(2);
        await Assert.ThrowsAsync<IOException>(() => fixture.PublicationContext!.PublishRealtimeAsync(first.Position, first).AsTask());
        await Assert.ThrowsAsync<IOException>(() => fixture.PublicationContext!.PublishRealtimeAsync(second.Position, second).AsTask());
        Assert.Single(fixture.Requests.Publications.Pending());
        Assert.Equal(1, fixture.PublicationCache!.Find(first.Position.Id.Trade)!.Read().Plan!.PlanRevision);
        fixture.FailBeforePublication = false;
        await fixture.PublicationContext!.PublishRealtimeAsync(second.Position, second);
        Assert.Equal(new long[] { 1, 2 }, fixture.Notifications.Select(value => value.Snapshot.Position!.PositionSequence));
    }
    [Fact]
    public async Task Outbox_write_failure_sends_nothing_and_retry_recovers_from_cache()
    {
        var path = DirectoryForTest();
        await using var fixture = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path, realPublication: true);
        var plan = Plan(1);
        // Block the pending frame path after the journal acquired its owner lease.
        var pendingDirectory = Path.Combine(path, "publications", "pending");
        Directory.Delete(pendingDirectory); File.WriteAllText(pendingDirectory, "blocked");
        await Assert.ThrowsAnyAsync<IOException>(() => fixture.PublicationContext!.PublishRealtimeAsync(plan.Position, plan).AsTask());
        Assert.Empty(fixture.Notifications);
        File.Delete(pendingDirectory); Directory.CreateDirectory(pendingDirectory);
        await fixture.PublicationContext!.PublishRealtimeAsync(plan.Position, plan);
        Assert.Single(fixture.Notifications);
    }
    [Fact]
    public async Task Corrupt_pending_publication_blocks_restart()
    {
        var path = DirectoryForTest();
        await using (var first = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path, realPublication: true) { FailBeforePublication = true })
        { var plan = Plan(1); await Assert.ThrowsAsync<IOException>(() => first.PublicationContext!.PublishRealtimeAsync(plan.Position, plan).AsTask()); }
        var file = Directory.GetFiles(Path.Combine(path, "publications", "pending"), "*.icp").Single();
        var bytes = File.ReadAllBytes(file); bytes[^1] ^= 1; File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidDataException>(() => new IronCondorPositionRequests(path));
    }
    [Fact]
    public async Task Same_publication_key_with_different_financial_payload_is_rejected()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false, realPublication: true);
        var plan = Plan(1); await fixture.PublicationContext!.PublishRealtimeAsync(plan.Position, plan);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PublicationContext.PublishRealtimeAsync(plan.Position, plan with { Explanation = "changed" }).AsTask());
        Assert.Single(fixture.Notifications);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Completion_write_or_archive_failure_remains_recoverable(bool archiveFailure)
    {
        var path = DirectoryForTest();
        await using var fixture = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path, realPublication: true);
        string blocked = "";
        fixture.AfterPublication = () =>
        {
            var key = fixture.Requests.Publications.Pending().Single().Key.ToString("N");
            blocked = archiveFailure
                ? Path.Combine(path, "publications", "sent", key[..2], key + ".icp")
                : Path.Combine(path, "publications", "pending", key + ".icp.tmp");
            Directory.CreateDirectory(blocked);
        };
        var plan = Plan(1);
        var failure = await Record.ExceptionAsync(() => fixture.PublicationContext!.PublishRealtimeAsync(plan.Position, plan).AsTask());
        Assert.True(failure is IOException or UnauthorizedAccessException);
        var original = fixture.Requests.Publications.Pending().Single();
        Assert.Equal(archiveFailure, original.Sent);
        Directory.Delete(blocked); fixture.AfterPublication = null;
        await fixture.PublicationContext!.RecoverPublicationsAsync();
        Assert.Equal(archiveFailure ? 1 : 2, fixture.Notifications.Count);
        Assert.All(fixture.Notifications, value => Assert.Equal(original.Event.Id, value.Id));
        Assert.Empty(fixture.Requests.Publications.Pending());
    }
    [Fact]
    public async Task Pending_send_does_not_acknowledge_source_before_transport_returns()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false, realPublication: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Publish = async notification =>
        { if (notification.Snapshot.Plan is not null) { entered.TrySetResult(); await release.Task; } };
        await fixture.Start(); var work = fixture.Deliver(fixture.Input("Position", 1));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(work.IsCompleted);
            Assert.Single(fixture.Requests.Publications.Pending());
        }
        finally { release.TrySetResult(); }
        Assert.True(await work); Assert.Empty(fixture.Requests.Publications.Pending());
    }
    [Fact]
    public async Task Older_source_cannot_publish_newer_cache_content_as_an_old_revision()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false, realPublication: true);
        var newer = Plan(2); await fixture.PublicationContext!.PublishRealtimeAsync(newer.Position, newer);
        var older = Plan(1); await fixture.PublicationContext.PublishRealtimeAsync(older.Position, older);
        Assert.Single(fixture.Notifications);
    }
    [Fact]
    public async Task Slow_trade_does_not_block_an_unrelated_trade_publication()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false, realPublication: true);
        var first = Plan(1);
        var otherId = TomasAI.IFM.Domain.Trade.Shared.StrategyPositionId.Create(new(1, 1, 1, 2), TomasAI.IFM.Domain.Trade.Shared.TradeStrategyKind.IronCondor);
        var other = first with { Position = first.Position with { Id = otherId } };
        fixture.PublicationCache!.Register(new TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition
        { Id = otherId.Trade, StrategyKind = TomasAI.IFM.Domain.Trade.Shared.TradeStrategyKind.IronCondor }, other.ValueDate);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Publish = async notification =>
        { if (notification.EntityId == first.Position.Id.Trade) { entered.TrySetResult(); await release.Task; } };
        var slow = fixture.PublicationContext!.PublishRealtimeAsync(first.Position, first).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.PublicationContext.PublishRealtimeAsync(other.Position, other).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(slow.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await slow;
    }
}
