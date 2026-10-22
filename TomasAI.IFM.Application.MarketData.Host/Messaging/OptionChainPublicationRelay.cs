using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
namespace TomasAI.IFM.Application.MarketData.Host;
/// <summary>Bounded replaceable cache publications; bootstrap reconciliation repairs dropped updates.</summary>
public sealed class OptionChainPublicationRelay(TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache cache, MarketDataHostConnection connection, ILogger<OptionChainPublicationRelay> logger) : BackgroundService
{
    readonly Channel<OptionChainPublication> updates = Channel.CreateBounded<OptionChainPublication>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        cache.Published += OnPublished;
        try
        {
            var client = await connection.GetClientAsync(stoppingToken).ConfigureAwait(false);
            var memo = new Dictionary<string, (Guid Generation, long Version, OptionChainPublicationFrame[] Frames)>(StringComparer.Ordinal);
            var replayAt = DateTimeOffset.MinValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                var pending = new Dictionary<string, OptionChainPublication>(StringComparer.Ordinal);
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                wait.CancelAfter(TimeSpan.FromSeconds(2));
                try { await updates.Reader.WaitToReadAsync(wait.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
                while (updates.Reader.TryRead(out var update)) pending[update.LookupScope] = update;
                if (DateTimeOffset.UtcNow >= replayAt)
                {
                    var current = cache.Export();
                    foreach (var old in memo.Keys.Except(current.Select(x => x.LookupScope), StringComparer.Ordinal).ToArray()) memo.Remove(old);
                    foreach (var update in current) pending[update.LookupScope] = update;
                    replayAt = DateTimeOffset.UtcNow.AddSeconds(2);
                }
                foreach (var update in pending.Values)
                {
                    try
                    {
                        if (!memo.TryGetValue(update.LookupScope, out var encoded) || encoded.Generation != update.Snapshot.GenerationId || encoded.Version != update.Version)
                        {
                            memo.Remove(update.LookupScope);
                            var frames = OptionChainPublicationFrame.Encode(update);
                            while (memo.Count > 0 && memo.Values.Sum(x => (long)x.Frames[0].Length) + frames[0].Length > 16 * 1024 * 1024)
                                memo.Remove(memo.Keys.First());
                            memo[update.LookupScope] = encoded = (update.Snapshot.GenerationId, update.Version, frames);
                        }
                        foreach (var frame in encoded.Frames)
                            await client.Connection.PublishAsync(connection.Scope + ".option-cache", frame,
                                serializer: NatsMessagePackSerializer<OptionChainPublicationFrame>.Default, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                    { logger.LogWarning(error, "Prepared option-cache publication failed; replay will retry. Scope={Scope}", update.LookupScope); }
                }
            }
        }
        finally { cache.Published -= OnPublished; }
    }
    void OnPublished(OptionChainPublication publication) => updates.Writer.TryWrite(publication);
}
