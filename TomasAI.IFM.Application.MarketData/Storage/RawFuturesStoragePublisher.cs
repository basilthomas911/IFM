using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.MarketData.Storage;

/// <summary>Colocated source spool publisher. Retains data until a durable broker acknowledgement.</summary>
public sealed class RawFuturesStoragePublisher(RawFuturesStorageOptions options, RawFuturesStorageTransport transport,
    ILogger<RawFuturesStoragePublisher> logger) : BackgroundService
{
    public string? Failure { get; private set; }
    public bool Ready { get; private set; }
    public long PublishedRecords { get; private set; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) { Ready = true; return; }
        options.Validate(); System.IO.Directory.CreateDirectory(options.Directory);
        using var owner = new FileStream(Path.Combine(options.Directory, ".publisher.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var health = transport.RespondToHealthAsync(() => Ready, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (health.IsFaulted) await health.ConfigureAwait(false);
                await transport.GetAsync(stoppingToken).ConfigureAwait(false);
                var progressed = false;
                foreach (var path in System.IO.Directory.EnumerateFiles(options.Directory, "*.wal").Order(StringComparer.Ordinal))
                {
                    var (batch, complete) = await RawFuturesJournal.ReadAsync(path, options.BatchCount, stoppingToken).ConfigureAwait(false);
                    if (batch is not null)
                    {
                        using var encoded = new TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.TradeBatchBinaryBuffer();
                        MessagePackSerializer.Serialize(encoded, batch, cancellationToken: stoppingToken);
                        await transport.PublishAsync(encoded.WrittenSpan.ToArray(),
                            $"{batch.SourceEpoch:N}:{batch.Records[0].Ordinal}:{batch.Records[^1].Ordinal}", stoppingToken).ConfigureAwait(false);
                        RawFuturesJournal.CommitCursor(path, new(batch.SourceEpoch, batch.EndOffset, batch.Records[^1].Ordinal));
                        PublishedRecords += batch.Records.Length;
                        progressed = true;
                    }
                    if (complete) RawFuturesJournal.RemoveCompleted(path);
                }
                Failure = null; Ready = true;
                if (progressed) { await Task.Yield(); continue; }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                Failure = error.Message; Ready = false;
                logger.LogError(error, "Raw futures publication failed; source spool is retained");
            }
            await Task.Delay(Failure is null ? options.FlushMilliseconds : 1000, stoppingToken).ConfigureAwait(false);
        }
        try { await health.ConfigureAwait(false); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
