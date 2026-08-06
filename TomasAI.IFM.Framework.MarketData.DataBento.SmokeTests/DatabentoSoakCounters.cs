using System.Collections.Concurrent;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

internal sealed class DatabentoSoakCounters
{
    private readonly Dictionary<InstrumentKey, long> _ticksByInstrument = [];
    private readonly ConcurrentQueue<string> _exceptionMessages = [];
    private readonly DatabentoTickCsvCapture? _csvCapture;
    private long _batches;
    private long _ticks;
    private long _quotes;
    private long _trades;
    private long _mboUpdates;
    private long _unknownInstruments;
    private long _unexpectedRecordKinds;
    private long _exceptions;

    internal DatabentoSoakCounters(
        IEnumerable<InstrumentKey> expectedInstruments,
        DatabentoTickCsvCapture? csvCapture = null)
    {
        _csvCapture = csvCapture;
        foreach (var instrument in expectedInstruments.ToHashSet())
        {
            _ticksByInstrument.Add(instrument, 0);
        }
    }

    internal long Batches => Interlocked.Read(ref _batches);
    internal long Ticks => Interlocked.Read(ref _ticks);
    internal long Quotes => Interlocked.Read(ref _quotes);
    internal long Trades => Interlocked.Read(ref _trades);
    internal long MboUpdates => Interlocked.Read(ref _mboUpdates);
    internal long UnknownInstruments => Interlocked.Read(ref _unknownInstruments);
    internal long UnexpectedRecordKinds => Interlocked.Read(ref _unexpectedRecordKinds);
    internal long Exceptions => Interlocked.Read(ref _exceptions);
    internal long CapturedCsvRows => _csvCapture?.Rows ?? 0;
    internal long CapturedCsvBytes => _csvCapture?.Bytes ?? 0;
    internal string? CapturedCsvPath => _csvCapture?.FilePath;
    internal IReadOnlyCollection<string> ExceptionMessages => _exceptionMessages.ToArray();

    internal int ExpectedInstrumentCount => _ticksByInstrument.Count;
    internal int InstrumentsWithTicks => _ticksByInstrument.Count(pair => pair.Value > 0);

    internal IReadOnlyList<KeyValuePair<InstrumentKey, long>> GetInstrumentCounts() =>
        _ticksByInstrument
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key.PublisherId)
            .ThenBy(pair => pair.Key.InstrumentId)
            .ToArray();

    internal Task ConsumeAsync(ISynchronousBatchReader<MarketDataBatch64> reader) =>
        Task.Run(() =>
        {
            while (true)
            {
                MarketDataBatch64 batch;
                try
                {
                    batch = reader.Read(Timeout.InfiniteTimeSpan);
                }
                catch (EndOfStreamException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    RecordException("reader", exception);
                    return;
                }

                using (batch)
                {
                    Interlocked.Increment(ref _batches);
                    foreach (ref readonly var record in batch.Records)
                    {
                        var ordinal = Interlocked.Increment(ref _ticks);
                        _csvCapture?.Write(ordinal, record);
                        var instrument = new InstrumentKey(
                            record.Header.PublisherId,
                            record.Header.InstrumentId);
                        if (_ticksByInstrument.TryGetValue(instrument, out var count))
                        {
                            _ticksByInstrument[instrument] = count + 1;
                        }
                        else
                        {
                            Interlocked.Increment(ref _unknownInstruments);
                        }

                        switch (record.Header.RecordKind)
                        {
                            case MarketRecordKind.Quote:
                                Interlocked.Increment(ref _quotes);
                                break;
                            case MarketRecordKind.Trade:
                                Interlocked.Increment(ref _trades);
                                break;
                            case MarketRecordKind.Mbo:
                                Interlocked.Increment(ref _mboUpdates);
                                break;
                            default:
                                Interlocked.Increment(ref _unexpectedRecordKinds);
                                break;
                        }
                    }
                }
            }
        });

    internal void FlushCapture() => _csvCapture?.Flush();

    internal void RecordException(string operation, Exception exception)
    {
        Interlocked.Increment(ref _exceptions);
        _exceptionMessages.Enqueue(
            $"{DateTimeOffset.UtcNow:O} [{operation}] "
            + $"{exception.GetType().Name}: {exception.Message}");
    }
}
