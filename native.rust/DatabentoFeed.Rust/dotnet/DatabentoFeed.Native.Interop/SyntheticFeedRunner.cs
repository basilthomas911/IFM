using System.Runtime.InteropServices;
using System.Text;

namespace DatabentoFeed.Native.Interop;

public sealed record SyntheticRun(
    uint MappingCount,
    byte[] MappingBlob,
    TickerInstrumentMappingV1[] Mappings,
    MarketRecord64[] Records,
    StatsV1 Stats);

public static unsafe class SyntheticFeedRunner
{
    public static SyntheticRun Run(NativeApi api, uint recordCount, uint batchSize, bool captureRecords = true)
    {
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
            SymbolOffset = i * 4, SymbolLength = 4, InputSymbology = 1, DataKinds = Dbf.MarketDataQuote |
                Dbf.MarketDataTrade | Dbf.MarketDataMbo
        };

        nint feed = 0;
        MarketRecord64* nativeBuffer = null;
        try
        {
            fixed (byte* datasetPtr = dataset)
                Require(api.FeedCreate(&config, datasetPtr, (uint)dataset.Length, &feed), "create");
            fixed (byte* symbolsPtr = symbols)
                Require(api.SubscribeTickers(feed, subscriptions, 2, symbolsPtr,
                    (uint)symbols.Length, 2_000), "subscribe");
            Require(api.AllocateReadBuffer(feed, batchSize, &nativeBuffer), "allocate");
            Require(api.Start(feed, 2_000), "start");

            uint mappingCount = 0, mappingBytes = 0;
            Require(api.GetMappingCounts(feed, &mappingCount, &mappingBytes), "mapping counts");
            var mappings = new TickerInstrumentMappingV1[mappingCount];
            var mappingBlob = new byte[mappingBytes];
            fixed (TickerInstrumentMappingV1* mappingsPtr = mappings)
            fixed (byte* mappingBlobPtr = mappingBlob)
                Require(api.CopyMappings(feed, mappingsPtr, mappingCount, mappingBlobPtr,
                    mappingBytes), "copy mappings");
            Require(api.SetConsumerReady(feed, 2_000), "consumer ready");

            var records = captureRecords ? new MarketRecord64[recordCount] : [];
            uint consumed = 0;
            bool terminal = false;
            while (!terminal || consumed < recordCount)
            {
                WaitResultV1 wait = new() { StructSize = (uint)sizeof(WaitResultV1), AbiVersion = Dbf.AbiVersion };
                Require(api.Wait(feed, 5_000, &wait), "wait");
                if ((wait.Flags & Dbf.WaitFault) != 0)
                    throw new InvalidOperationException($"Native producer faulted with status {wait.TerminalStatus}.");
                terminal |= (wait.Flags & Dbf.WaitTerminal) != 0;
                if ((wait.Flags & Dbf.WaitData) == 0) continue;
                do
                {
                    BatchResultV1 batch = new() { StructSize = (uint)sizeof(BatchResultV1), AbiVersion = Dbf.AbiVersion };
                    Require(api.ReadBatch(feed, nativeBuffer, batchSize, &batch), "read");
                    if (captureRecords)
                        new ReadOnlySpan<MarketRecord64>(nativeBuffer, checked((int)batch.RecordsRead))
                            .CopyTo(records.AsSpan(checked((int)consumed)));
                    consumed += batch.RecordsRead;
                    if (batch.MoreAvailable == 0) break;
                } while (true);
            }
            if (consumed != recordCount) throw new InvalidOperationException($"Expected {recordCount} records; read {consumed}.");
            StatsV1 stats = new() { StructSize = (uint)sizeof(StatsV1), AbiVersion = Dbf.AbiVersion };
            Require(api.GetStats(feed, &stats), "stats");
            Require(api.Stop(feed, 2_000), "stop");
            return new(mappingCount, mappingBlob, mappings, records, stats);
        }
        finally
        {
            if (nativeBuffer != null && feed != 0) api.FreeReadBuffer(feed, nativeBuffer);
            if (feed != 0) api.Destroy(feed);
        }
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
