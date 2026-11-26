using System.Security.Cryptography;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Finite original external-call transcript. Admission releases a recorded reply; no price or financial result is calculated.</summary>
public sealed class RecordedMarketDataBackend
{
    readonly Lock gate = new();
    readonly Dictionary<string, Queue<RecordedMarketDataExchange>> admitted = new(StringComparer.Ordinal);
    readonly Dictionary<string, LinkedList<Pending>> waiting = new(StringComparer.Ordinal);
    readonly long maximumBytes;
    readonly int maximumCalls;
    readonly int maximumWaiters;
    MarketDataHostReply snapshot;
    MarketDataHostReply status;
    MarketDataHostReply ready;
    string? snapshotOrigin, statusOrigin, readyOrigin;
    long bytes, accepted, consumed, stateUpdates, stateReads, missing;
    int queued, pending, delivering;
    bool complete;
    string? failure;
    sealed class Pending(string key)
    {
        public string Key { get; } = key;
        public TaskCompletionSource<RecordedMarketDataResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Pending>? Node;
    }
    public RecordedMarketDataBackend(MarketDataReadSnapshot baseline, long maximumBytes = 128L * 1024 * 1024,
        int maximumCalls = 16384, int maximumWaiters = 256)
        : this(MarketDataHostConnection.Encode(baseline), maximumBytes, maximumCalls, maximumWaiters) { }
    public RecordedMarketDataBackend(byte[] originalBaselineWire, long maximumBytes = 128L * 1024 * 1024,
        int maximumCalls = 16384, int maximumWaiters = 256)
    {
        if (originalBaselineWire.Length is 0 or > 16 * 1024 * 1024 || maximumBytes is < 1 or > 1024L * 1024 * 1024
            || maximumCalls is < 1 or > 1000000 || maximumWaiters is < 1 or > 4096)
            throw new InvalidDataException("Invalid finite original backend transcript bounds.");
        this.maximumBytes = maximumBytes; this.maximumCalls = maximumCalls; this.maximumWaiters = maximumWaiters;
        var baseline = MarketDataHostConnection.Decode<MarketDataReadSnapshot>(originalBaselineWire);
        snapshot = new(originalBaselineWire.ToArray(), null);
        status = new(MarketDataHostConnection.Encode(baseline.Status), null);
        ready = new(MarketDataHostConnection.Encode(baseline.Status.Ready), null);
    }
    public long RecordedExchanges { get { lock (gate) return accepted; } }
    public RecordedMarketDataProgress Diagnostics { get { lock (gate) return new(accepted, consumed, stateUpdates, stateReads, queued, pending, delivering, bytes, missing, complete, failure); } }
    public static string RequestKey(MarketDataHostRequest request)
        => request.Path + "|" + Convert.ToHexStringLower(SHA256.HashData(request.Payload));

    /// <summary>Release one original completed call at its recorded arrival barrier. Each non-state reply is consumed once, in admission order per exact payload.</summary>
    public void Accept(RecordedMarketDataExchange exchange)
    {
        if (exchange.StartedAtUtc.Offset != TimeSpan.Zero || exchange.CompletedAtUtc.Offset != TimeSpan.Zero || exchange.CompletedAtUtc < exchange.StartedAtUtc
            || exchange.Request.Path.Length is 0 or > 512 || exchange.Request.Payload.Length > 16 * 1024 * 1024 || exchange.Reply.Payload.Length > 16 * 1024 * 1024
            || exchange.ReplyOrigin?.Length > 65536 || exchange.RequestOrigin?.Length > 65536 || exchange.Reply.Error?.Length > 65536 || exchange.TransportFailure?.Length > 256)
            throw new InvalidDataException("Invalid original backend exchange bounds/times.");
        lock (gate)
        {
            if (complete) throw Error("Original transcript admission is already complete.");
            if (exchange.TransportFailure is { } transportFailure)
            { accepted++; throw Error("Original backend transport failure cannot be replaced by a synthetic reply: " + transportFailure); }
            // Freeze caller-owned arrays; no later mutation can change an admitted original result.
            exchange = exchange with { Request = exchange.Request with { Payload = exchange.Request.Payload.ToArray() },
                Reply = exchange.Reply with { Payload = exchange.Reply.Payload.ToArray() } };
            if (StatePath(exchange.Request.Path))
            {
                ApplyState(exchange); accepted++; stateUpdates++; return;
            }
            var size = Charge(exchange);
            if (size > maximumBytes - bytes || queued + delivering >= maximumCalls)
                throw Error("Original backend replay transcript capacity exceeded; no required input was evicted.");
            var key = RequestKey(exchange.Request);
            bytes += size; accepted++;
            if (waiting.TryGetValue(key, out var requests) && requests.First is { } first)
            {
                var request = first.Value; requests.RemoveFirst(); request.Node = null; pending--;
                if (requests.Count == 0) waiting.Remove(key);
                request.Completion.TrySetResult(Consume(exchange, size));
            }
            else
            {
                if (!admitted.TryGetValue(key, out var calls)) admitted[key] = calls = new();
                calls.Enqueue(exchange); queued++;
            }
        }
    }
    static long Charge(RecordedMarketDataExchange value) => checked(2048L + value.Request.Payload.Length + value.Reply.Payload.Length
        + 2L * (value.Request.Path.Length + (value.RequestOrigin?.Length ?? 0) + (value.ReplyOrigin?.Length ?? 0) + (value.Reply.Error?.Length ?? 0)));
    static bool StatePath(string path) => path is "ready" or "api/GetBackendReadSnapshotAsync" or "api/GetBackendStatusAsync";
    void ApplyState(RecordedMarketDataExchange exchange)
    {
        var reply = exchange.Reply;
        if (exchange.Request.Path == "api/GetBackendReadSnapshotAsync")
        {
            if (reply.Error is null)
            {
                var value = MarketDataHostConnection.Decode<MarketDataReadSnapshot>(reply.Payload);
                status = new(MarketDataHostConnection.Encode(value.Status), null);
                ready = new(MarketDataHostConnection.Encode(value.Status.Ready), null);
            }
            else status = ready = new([], reply.Error);
            snapshot = reply; snapshotOrigin = statusOrigin = readyOrigin = exchange.ReplyOrigin;
        }
        else if (exchange.Request.Path == "api/GetBackendStatusAsync")
        {
            ready = reply.Error is null ? new(MarketDataHostConnection.Encode(MarketDataHostConnection.Decode<MarketDataBackendStatus>(reply.Payload).Ready), null) : new([], reply.Error);
            status = reply; statusOrigin = readyOrigin = exchange.ReplyOrigin;
        }
        else { ready = reply; readyOrigin = exchange.ReplyOrigin; }
    }
    RecordedMarketDataResult Consume(RecordedMarketDataExchange exchange, long size)
    {
        consumed++; delivering++;
        return new(exchange.Reply, exchange.ReplyOrigin, false, () => { lock (gate) { bytes -= size; delivering--; } });
    }
    /// <summary>Non-state operations wait for their original exchange, bounded by the actual caller's cancellation/deadline.</summary>
    public Task<RecordedMarketDataResult> ReplyAsync(MarketDataHostRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Path.Length is 0 or > 512 || request.Payload.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Invalid original backend request bounds.");
        lock (gate)
        {
            if (StatePath(request.Path))
            {
                stateReads++;
                return Task.FromResult(request.Path switch
                {
                    "ready" => new RecordedMarketDataResult(ready, readyOrigin, true),
                    "api/GetBackendStatusAsync" => new RecordedMarketDataResult(status, statusOrigin, true),
                    _ => new RecordedMarketDataResult(snapshot, snapshotOrigin, true)
                });
            }
            var key = RequestKey(request);
            if (admitted.TryGetValue(key, out var calls) && calls.Count != 0)
            {
                var exchange = calls.Dequeue(); queued--; if (calls.Count == 0) admitted.Remove(key);
                return Task.FromResult(Consume(exchange, Charge(exchange)));
            }
            if (complete) { missing++; throw Error("REPLAY_ORIGINAL_EXTERNAL_INPUT_UNAVAILABLE: " + request.Path); }
            if (pending >= maximumWaiters) throw Error("Original backend replay waiter capacity exceeded.");
            var requestWaiter = new Pending(key);
            if (!waiting.TryGetValue(key, out var requests)) waiting[key] = requests = new();
            requestWaiter.Node = requests.AddLast(requestWaiter); pending++;
            return WaitAsync(requestWaiter, cancellationToken);
        }
    }
    async Task<RecordedMarketDataResult> WaitAsync(Pending request, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() =>
        {
            lock (gate)
            {
                if (request.Node is not { List: { } requests } node) return;
                requests.Remove(node); request.Node = null; pending--; missing++;
                if (requests.Count == 0) waiting.Remove(request.Key);
                failure ??= "Original backend reply was unavailable before the physical caller deadline/cancellation.";
                request.Completion.TrySetCanceled(cancellationToken);
            }
        });
        return await request.Completion.Task.ConfigureAwait(false);
    }
    /// <summary>Marks EOF. Unmatched requests fail explicitly instead of manufacturing a No Trade input.</summary>
    public void CompleteTranscript()
    {
        lock (gate)
        {
            complete = true;
            foreach (var requests in waiting.Values) foreach (var request in requests)
            {
                request.Node = null; missing++;
                request.Completion.TrySetException(new InvalidDataException("REPLAY_ORIGINAL_EXTERNAL_INPUT_UNAVAILABLE at transcript EOF."));
            }
            if (pending != 0) failure ??= "Original transcript ended with unmatched backend requests.";
            waiting.Clear(); pending = 0;
        }
    }
    internal void RecordTransportFailure(string reason) { lock (gate) failure ??= reason; }
    IOException Error(string message) { failure ??= message; return new(message); }
    public void VerifyComplete()
    {
        lock (gate)
            if (!complete || failure is not null || pending != 0 || queued != 0 || delivering != 0 || missing != 0)
                throw new InvalidDataException($"Original backend transcript incomplete: accepted={accepted}, consumed={consumed}, stateUpdates={stateUpdates}, queued={queued}, waiting={pending}, delivering={delivering}, missing={missing}, sealed={complete}; {failure}");
    }
}

/// <summary>Reply bytes and provenance are selected atomically; disposal releases their bounded transcript charge after transport settlement.</summary>
public sealed class RecordedMarketDataResult(MarketDataHostReply reply, string? origin, bool derivedState, Action? release = null) : IDisposable
{
    Action? release = release;
    public MarketDataHostReply Reply { get; } = reply;
    public string? Origin { get; } = origin;
    public bool DerivedState { get; } = derivedState;
    public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
}
public sealed record RecordedMarketDataProgress(long AcceptedExchanges, long ConsumedExchanges, long StateUpdates,
    long DerivedStateReads, int UnconsumedExchanges, int PendingRequests, int DeliveringReplies, long RetainedTranscriptBytes,
    long MissingRequests, bool TranscriptComplete, string? Failure);
