using System.Text;

namespace DatabentoFeed.Native.Interop;

/// <summary>
/// Prepares a finite synthetic feed so benchmarks can exclude lifecycle work from their
/// timed regions and independently measure native publication and P/Invoke draining.
/// </summary>
public sealed unsafe class PreparedSyntheticFeed : IDisposable
{
    private readonly NativeApi _api;
    private nint _feed;
    private MarketRecord64* _buffer;
    private readonly uint _batchSize;

    public PreparedSyntheticFeed(NativeApi api, uint recordCount, uint batchSize)
    {
        _api = api;
        _batchSize = batchSize;
        byte[] dataset = Encoding.UTF8.GetBytes("SYNTHETIC");
        byte[] symbols = Encoding.UTF8.GetBytes("ESM6NQM6");
        FeedConfigV1 config = new()
        {
            StructSize = (uint)sizeof(FeedConfigV1), AbiVersion = Dbf.AbiVersion,
            DataSource = Dbf.DataSourceSynthetic, FeedKind = Dbf.FeedTicker,
            RingMemoryBytes = Math.Max(1UL << 20, NextPowerOfTwo((ulong)recordCount * 64)),
            SpinIterations = 256, RingFullTimeoutUs = 250_000,
            SyntheticRecordCount = recordCount, SyntheticInstrumentCount = 2,
            HeartbeatIntervalMs = 5_000, ProducerLogicalProcessor = ushort.MaxValue,
            DrainLogicalProcessor = ushort.MaxValue, DatasetLength = (uint)dataset.Length,
            SyntheticStartSequence = 1
        };
        TickerSubscriptionV1* subscriptions = stackalloc TickerSubscriptionV1[2];
        for (uint i = 0; i < 2; i++) subscriptions[i] = new()
        {
            StructSize = (uint)sizeof(TickerSubscriptionV1), AbiVersion = Dbf.AbiVersion,
            SymbolOffset = i * 4, SymbolLength = 4, InputSymbology = 1,
            DataKinds = Dbf.MarketDataQuote | Dbf.MarketDataTrade | Dbf.MarketDataMbo
        };

        try
        {
            nint feed = 0;
            fixed (byte* datasetPtr = dataset)
                Require(_api.FeedCreate(&config, datasetPtr, (uint)dataset.Length, &feed), "create");
            _feed = feed;
            fixed (byte* symbolsPtr = symbols)
                Require(_api.SubscribeTickers(_feed, subscriptions, 2, symbolsPtr,
                    (uint)symbols.Length, 2_000), "subscribe");
            MarketRecord64* buffer = null;
            Require(_api.AllocateReadBuffer(_feed, batchSize, &buffer), "allocate");
            _buffer = buffer;
            Require(_api.Start(_feed, 2_000), "start");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Releases the already-created producer and polls until native publication finishes.</summary>
    public ulong PublishAll()
    {
        Require(_api.SetConsumerReady(_feed, 2_000), "consumer ready");
        StatsV1 stats = default;
        while (true)
        {
            stats = new() { StructSize = (uint)sizeof(StatsV1), AbiVersion = Dbf.AbiVersion };
            Require(_api.GetStats(_feed, &stats), "stats");
            if (stats.State is Dbf.StateStopped or Dbf.StateFaulted) break;
            Thread.Sleep(1);
        }
        if (stats.State == Dbf.StateFaulted)
            throw new InvalidOperationException($"Native producer faulted with status {stats.TerminalStatus}.");
        return stats.RecordsProduced;
    }

    /// <summary>Publishes all configured records before a consumer-drain measurement begins.</summary>
    public void Prefill() => PublishAll();

    /// <summary>Drains the prefilled ring through the canonical wait/read P/Invoke calls.</summary>
    public ulong DrainAll()
    {
        ulong consumed = 0;
        while (true)
        {
            WaitResultV1 wait = new() { StructSize = (uint)sizeof(WaitResultV1), AbiVersion = Dbf.AbiVersion };
            Require(_api.Wait(_feed, 0, &wait), "wait");
            if ((wait.Flags & Dbf.WaitFault) != 0)
                throw new InvalidOperationException($"Native feed faulted with status {wait.TerminalStatus}.");
            if ((wait.Flags & Dbf.WaitData) != 0)
            {
                BatchResultV1 batch = new() { StructSize = (uint)sizeof(BatchResultV1), AbiVersion = Dbf.AbiVersion };
                Require(_api.ReadBatch(_feed, _buffer, _batchSize, &batch), "read");
                consumed += batch.RecordsRead;
            }
            if ((wait.Flags & Dbf.WaitTerminal) != 0 && wait.AvailableRecords == 0) return consumed;
        }
    }

    public void Dispose()
    {
        if (_feed == 0) return;
        _api.Stop(_feed, 2_000);
        if (_buffer != null) _api.FreeReadBuffer(_feed, _buffer);
        _api.Destroy(_feed);
        _buffer = null;
        _feed = 0;
    }

    private static ulong NextPowerOfTwo(ulong value)
    {
        value--;
        value |= value >> 1; value |= value >> 2; value |= value >> 4;
        value |= value >> 8; value |= value >> 16; value |= value >> 32;
        return value + 1;
    }

    private static void Require(int status, string operation)
    {
        if (status != Dbf.Ok) throw new InvalidOperationException($"Native {operation} returned {status}.");
    }
}
