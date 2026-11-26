using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class RecordedMarketDataBackendTests
{
    static MarketDataReadSnapshot Snapshot(bool ready) => new(new(ready, new(2026, 11, 25), []), [], [], []);
    static readonly DateTimeOffset At = new(2026, 11, 25, 14, 30, 0, TimeSpan.Zero);
    static MarketDataHostRequest Request(string path = "api/ReadEconomicCalendarAsync", byte payload = 1)
        => new(path, [payload], Guid.NewGuid(), DateTime.UtcNow.AddSeconds(3));
    static RecordedMarketDataExchange Exchange(MarketDataHostRequest request, byte reply = 9, string? origin = null)
        => new(request, new([reply], null), At, At.AddMilliseconds(10), origin);

    [Fact]
    public async Task Original_reply_and_origin_are_atomic_and_each_call_is_consumed_once()
    {
        var backend = new RecordedMarketDataBackend(Snapshot(true)); var request = Request();
        var origin = MessageOriginBinary.Serialize(new(At, "recorded-original", causationId: Guid.NewGuid()));
        backend.Accept(Exchange(request, origin: origin));
        using (var result = await backend.ReplyAsync(request with { OperationId = Guid.NewGuid() }))
        {
            Assert.Equal(new byte[] { 9 }, result.Reply.Payload); Assert.Equal(origin, result.Origin); Assert.False(result.DerivedState);
            var laterOrigin = MessageOriginBinary.Serialize(new(At.AddSeconds(1), "later-original"));
            backend.Accept(Exchange(request, 10, laterOrigin));
            Assert.Equal(new byte[] { 9 }, result.Reply.Payload); Assert.Equal(origin, result.Origin);
        }
        using (var next = await backend.ReplyAsync(request)) Assert.Equal(new byte[] { 10 }, next.Reply.Payload);
        backend.CompleteTranscript(); backend.VerifyComplete();
        await Assert.ThrowsAsync<IOException>(() => backend.ReplyAsync(request));
        Assert.Equal(1, backend.Diagnostics.MissingRequests); Assert.Throws<InvalidDataException>(backend.VerifyComplete);
    }
    [Fact]
    public async Task Requests_wait_until_original_admission_and_identical_calls_keep_fifo_order()
    {
        var backend = new RecordedMarketDataBackend(Snapshot(true)); var request = Request();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var first = backend.ReplyAsync(request, stop.Token); var second = backend.ReplyAsync(request, stop.Token);
        Assert.False(first.IsCompleted); Assert.False(second.IsCompleted); Assert.Equal(2, backend.Diagnostics.PendingRequests);
        backend.Accept(Exchange(request, 20));
        using (var reply = await first) Assert.Equal(new byte[] { 20 }, reply.Reply.Payload);
        Assert.False(second.IsCompleted); backend.Accept(Exchange(request, 21));
        using (var reply = await second) Assert.Equal(new byte[] { 21 }, reply.Reply.Payload);
        backend.CompleteTranscript(); backend.VerifyComplete();
    }
    [Fact]
    public async Task Cancellation_wrong_payload_and_unconsumed_originals_cannot_qualify()
    {
        var backend = new RecordedMarketDataBackend(Snapshot(true)); var request = Request();
        using var stop = new CancellationTokenSource(); var waiting = backend.ReplyAsync(request with { Payload = [2] }, stop.Token);
        backend.Accept(Exchange(request)); Assert.False(waiting.IsCompleted); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        backend.CompleteTranscript(); Assert.Equal(1, backend.Diagnostics.UnconsumedExchanges); Assert.Equal(1, backend.Diagnostics.MissingRequests);
        Assert.Throws<InvalidDataException>(backend.VerifyComplete);
        using var original = await backend.ReplyAsync(request); Assert.Equal(new byte[] { 9 }, original.Reply.Payload);
    }
    [Fact]
    public async Task Eof_fails_unrecorded_waiters_without_generating_a_provider_result()
    {
        var backend = new RecordedMarketDataBackend(Snapshot(true)); var waiting = backend.ReplyAsync(Request());
        backend.CompleteTranscript(); await Assert.ThrowsAsync<InvalidDataException>(() => waiting);
        Assert.Equal(1, backend.Diagnostics.MissingRequests); Assert.Equal(0, backend.Diagnostics.PendingRequests);
        Assert.Throws<InvalidDataException>(backend.VerifyComplete);
    }
    [Fact]
    public async Task Initial_original_snapshot_bytes_survive_without_reencoding_unknown_fields()
    {
        var known = MarketDataHostConnection.Encode(Snapshot(true)); Assert.InRange(known[0], 0x90, 0x9e);
        var original = known.Concat(new byte[] { 0xc0 }).ToArray(); original[0]++;
        var backend = new RecordedMarketDataBackend(original);
        using var reply = await backend.ReplyAsync(Request("api/GetBackendReadSnapshotAsync"));
        Assert.Equal(original, reply.Reply.Payload); Assert.True(reply.DerivedState);
        backend.CompleteTranscript(); backend.VerifyComplete(); Assert.Equal(1, backend.Diagnostics.DerivedStateReads);
    }
    [Fact]
    public async Task Failed_newer_snapshot_does_not_return_an_older_healthy_ready_value()
    {
        var backend = new RecordedMarketDataBackend(Snapshot(true)); var request = Request("api/GetBackendReadSnapshotAsync");
        backend.Accept(new(request, new([], "original failure"), At, At, null));
        using (var failed = await backend.ReplyAsync(Request("ready"))) Assert.Equal("original failure", failed.Reply.Error);
        backend.Accept(new(request, new(MarketDataHostConnection.Encode(Snapshot(false)), null), At, At, null));
        using (var current = await backend.ReplyAsync(request)) Assert.False(MarketDataHostConnection.Decode<MarketDataReadSnapshot>(current.Reply.Payload).Status.Ready);
        backend.CompleteTranscript(); backend.VerifyComplete();
    }
    [Fact]
    public async Task Payload_metadata_waiter_and_inflight_bounds_are_enforced_without_eviction()
    {
        var metadata = new RecordedMarketDataBackend(Snapshot(true), maximumBytes: 4096);
        Assert.Throws<IOException>(() => metadata.Accept(Exchange(Request(), origin: new string('x', 4096)))); Assert.Equal(0, metadata.RecordedExchanges);
        var backend = new RecordedMarketDataBackend(Snapshot(true), maximumCalls: 1, maximumWaiters: 1); var request = Request();
        backend.Accept(Exchange(request)); using (var result = await backend.ReplyAsync(request))
        { Assert.True(backend.Diagnostics.RetainedTranscriptBytes > 0); Assert.Equal(1, backend.Diagnostics.DeliveringReplies); Assert.Throws<IOException>(() => backend.Accept(Exchange(request))); }
        Assert.Equal(0, backend.Diagnostics.RetainedTranscriptBytes);
        var waitingBackend = new RecordedMarketDataBackend(Snapshot(true), maximumWaiters: 1); using var stop = new CancellationTokenSource();
        var waiting = waitingBackend.ReplyAsync(Request(), stop.Token); await Assert.ThrowsAsync<IOException>(() => waitingBackend.ReplyAsync(Request(payload: 2))); stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
    [Fact]
    public void Original_transport_failure_is_explicit_and_never_becomes_a_successful_reply()
    {
        var backend = new RecordedMarketDataBackend(Snapshot(true));
        Assert.Throws<IOException>(() => backend.Accept(new(Request(), new([], null), At, At, null, "original-request-origin", "NoResponders")));
        Assert.Contains("transport failure", backend.Diagnostics.Failure); backend.CompleteTranscript(); Assert.Throws<InvalidDataException>(backend.VerifyComplete);
    }
}
