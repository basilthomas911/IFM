using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Explicit isolated original-input adapter. Waiting on one transcript call never blocks another request key.</summary>
internal sealed class RecordedMarketDataNatsServer : IAsyncDisposable
{
    readonly List<INatsSub<MarketDataHostRequest>> subscriptions;
    readonly Task[] loops;
    readonly CancellationTokenSource stopping;
    readonly SemaphoreSlim capacity = new(64);
    readonly object taskGate = new();
    readonly HashSet<Task> active = [];
    readonly RecordedMarketDataBackend backend;
    readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int disposing;
    RecordedMarketDataNatsServer(List<INatsSub<MarketDataHostRequest>> values, RecordedMarketDataBackend backend, CancellationToken token)
    {
        subscriptions = values; this.backend = backend; stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        loops = values.Select(async subscription =>
        {
            try
            {
                await foreach (var message in subscription.Msgs.ReadAllAsync(stopping.Token).ConfigureAwait(false))
                {
                    await capacity.WaitAsync(stopping.Token).ConfigureAwait(false);
                    var task = ProcessAsync(message);
                    lock (taskGate) active.Add(task);
                    _ = task.ContinueWith(completed => { lock (taskGate) active.Remove(completed); }, CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (Exception error) { backend.RecordTransportFailure("Original replay subscription failed: " + error.GetType().Name); throw; }
        }).ToArray();
    }
    async Task ProcessAsync(NatsMsg<MarketDataHostRequest> message)
    {
        try
        {
            if (message.Data is not { } request) { backend.RecordTransportFailure("Original replay request body was absent."); return; }
            using var physical = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
            if (request.Path != "ready" && !MarketDataHostConnection.IsRecoveryRequest(request.Path))
            {
                var remaining = request.DeadlineUtc - TimeProvider.System.GetUtcNow().UtcDateTime;
                if (remaining <= TimeSpan.Zero)
                {
                    backend.RecordTransportFailure("Original replay request arrived after its physical deadline.");
                    await ReplyErrorAsync(message, "Market-data request deadline expired.").ConfigureAwait(false); return;
                }
                physical.CancelAfter(remaining);
            }
            try
            {
                using var result = await backend.ReplyAsync(request, physical.Token).ConfigureAwait(false);
                var headers = result.Origin is { } origin
                    ? global::TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Add(null, MessageOriginBinary.Deserialize(origin)) : null;
                await message.ReplyAsync(result.Reply, headers: headers, serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default,
                    cancellationToken: stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (physical.IsCancellationRequested)
            {
                backend.RecordTransportFailure("Original replay backend request ended without its recorded reply.");
                if (!stopping.IsCancellationRequested) await ReplyErrorAsync(message, "REPLAY_ORIGINAL_EXTERNAL_INPUT_UNAVAILABLE before physical deadline.").ConfigureAwait(false);
            }
            catch (Exception error)
            {
                backend.RecordTransportFailure("Original replay backend response failed: " + error.GetType().Name);
                if (!stopping.IsCancellationRequested) await ReplyErrorAsync(message, "REPLAY_ORIGINAL_EXTERNAL_INPUT_UNAVAILABLE; replay is incomplete.").ConfigureAwait(false);
            }
        }
        catch (Exception error) { backend.RecordTransportFailure("Original replay reply transport failed: " + error.GetType().Name); }
        finally { capacity.Release(); }
    }
    Task ReplyErrorAsync(NatsMsg<MarketDataHostRequest> message, string error)
        => message.ReplyAsync(new MarketDataHostReply([], error), serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default,
            cancellationToken: stopping.Token).AsTask();
    public static async Task<RecordedMarketDataNatsServer> StartAsync(NatsClient client, string fullScope,
        RecordedMarketDataBackend backend, CancellationToken token = default)
    {
        if (!Uri.TryCreate(client.Connection.Opts.Url, UriKind.Absolute, out var broker) || broker.Scheme != "nats"
            || broker.Host != "127.0.0.1" || broker.Port <= 1024 || broker.Port == 4222 || broker.UserInfo.Length > 0)
            throw new InvalidOperationException("Original-input replay requires an explicit isolated loopback broker.");
        if (string.IsNullOrWhiteSpace(fullScope) || fullScope.Any(value => !(char.IsAsciiLetterOrDigit(value) || value is '.' or '-')))
            throw new ArgumentException("An exact replay scope is required.", nameof(fullScope));
        var values = new List<INatsSub<MarketDataHostRequest>>();
        try
        {
            foreach (var suffix in new[] { ".observe", ".control", ".stop" })
                values.Add(await client.Connection.SubscribeCoreAsync<MarketDataHostRequest>(fullScope + suffix,
                    serializer: NatsMessagePackSerializer<MarketDataHostRequest>.Default,
                    opts: new NatsSubOpts { ChannelOpts = new NatsSubChannelOpts { Capacity = 64,
                        FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait } }, cancellationToken: token).ConfigureAwait(false));
            await client.PingAsync(token).ConfigureAwait(false);
            return new(values, backend, token);
        }
        catch { foreach (var value in values) await value.DisposeAsync().ConfigureAwait(false); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposing, 1) == 0)
        {
            try
            {
                stopping.Cancel();
                Exception? failure = null;
                foreach (var value in subscriptions)
                    try { await value.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { failure ??= error; }
                try { await Task.WhenAll(loops).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception error) { failure ??= error; }
                Task[] pending; lock (taskGate) pending = active.ToArray();
                try { await Task.WhenAll(pending).ConfigureAwait(false); } catch (Exception error) { failure ??= error; }
                capacity.Dispose(); stopping.Dispose();
                if (failure is not null) throw failure;
                disposed.TrySetResult();
            }
            catch (Exception error) { disposed.TrySetException(error); }
        }
        await disposed.Task.ConfigureAwait(false);
    }
}
