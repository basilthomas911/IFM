using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class NativeLifecycleTests
{
    [Fact]
    public unsafe void SyntheticFeedDrainsAllRecordsInOrder()
    {
        const uint recordCount = 4_096;
        using var feed = CreateSubscribedFeed(recordCount);
        MarketRecord64* readBuffer = null;
        NativeTest.Require(NativeMethods.FeedAllocateReadBuffer(feed, 512, out readBuffer));

        try
        {
            NativeTest.Require(NativeMethods.FeedStart(feed, 2_000));
            NativeTest.Require(NativeMethods.FeedGetTickerMappingCounts(
                feed, out var mappingCount, out var blobBytes));
            Assert.Equal(2u, mappingCount);

            var mappings = new NativeTickerInstrumentMapping[mappingCount];
            var strings = new byte[blobBytes];
            fixed (NativeTickerInstrumentMapping* mappingPointer = mappings)
            fixed (byte* stringPointer = strings)
            {
                NativeTest.Require(NativeMethods.FeedCopyTickerMappings(
                    feed,
                    mappingPointer,
                    mappingCount,
                    stringPointer,
                    blobBytes));
            }
            Assert.Equal(1u, mappings[0].InstrumentId);
            Assert.Equal(2u, mappings[1].InstrumentId);

            NativeTest.Require(NativeMethods.FeedSetConsumerReady(feed, 2_000));
            uint lastSequence = 0;
            ulong drained = 0;
            var terminal = false;
            while (!terminal || drained < recordCount)
            {
                var wait = NativeTest.CreateWaitResult();
                NativeTest.Require(NativeMethods.FeedWait(feed, 5_000, ref wait));
                if ((wait.Flags & NativeWaitFlags.Data) != 0)
                {
                    do
                    {
                        var batch = NativeTest.CreateBatchResult();
                        NativeTest.Require(NativeMethods.FeedReadBatch(
                            feed, readBuffer, 512, ref batch));
                        for (var index = 0u; index < batch.RecordsRead; index++)
                        {
                            var record = readBuffer[index];
                            Assert.Equal(lastSequence + 1, record.Header.Sequence);
                            lastSequence = record.Header.Sequence;
                            Assert.Equal(((record.Header.Sequence - 1) % 2) + 1,
                                record.Header.InstrumentId);
                        }
                        drained += batch.RecordsRead;
                        if (batch.MoreAvailable == 0)
                        {
                            break;
                        }
                    } while (true);
                }
                terminal = (wait.Flags & NativeWaitFlags.Terminal) != 0;
            }

            Assert.Equal(recordCount, drained);
            var stats = NativeTest.CreateStats();
            NativeTest.Require(NativeMethods.FeedGetStats(feed, ref stats));
            Assert.Equal(recordCount, stats.RecordsProduced);
            Assert.Equal(recordCount, stats.RecordsConsumed);
            Assert.Equal(16_384ul, stats.RingCapacityRecords);

            NativeTest.Require(NativeMethods.FeedStop(feed, 2_000));
        }
        finally
        {
            if (readBuffer != null)
            {
                NativeTest.Require(NativeMethods.FeedFreeReadBuffer(feed, readBuffer));
            }
        }
    }

    private static unsafe SafeDbFeedHandle CreateSubscribedFeed(uint recordCount)
    {
        var dataset = Encoding.UTF8.GetBytes("SYNTHETIC");
        var config = NativeTest.CreateConfig(recordCount, (uint)dataset.Length);
        nint rawHandle;
        fixed (byte* datasetPointer = dataset)
        {
            NativeTest.Require(NativeMethods.FeedCreate(
                &config,
                datasetPointer,
                (uint)dataset.Length,
                out rawHandle));
        }

        var handle = new SafeDbFeedHandle(rawHandle);
        try
        {
            var symbols = Encoding.UTF8.GetBytes("ESM6NQM6");
            var subscriptions = new[]
            {
                NativeTest.CreateSubscription(0),
                NativeTest.CreateSubscription(4)
            };
            fixed (NativeTickerSubscription* subscriptionPointer = subscriptions)
            fixed (byte* symbolPointer = symbols)
            {
                NativeTest.Require(NativeMethods.FeedSubscribeTickers(
                    handle,
                    subscriptionPointer,
                    (uint)subscriptions.Length,
                    symbolPointer,
                    (uint)symbols.Length,
                    1_000));
            }
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }
}

internal static class NativeTest
{
    internal static void Require(
        DatabentoFeedStatus actual,
        DatabentoFeedStatus expected = DatabentoFeedStatus.Ok) =>
        Assert.Equal(expected, actual);

    internal static unsafe NativeFeedConfig CreateConfig(
        uint recordCount,
        uint datasetLength)
    {
        var config = new NativeFeedConfig
        {
            StructSize = (uint)sizeof(NativeFeedConfig),
            AbiVersion = NativeConstants.AbiVersion,
            DataSource = 1,
            FeedKind = 1,
            RingMemoryBytes = 1u << 20,
            SpinIterations = 256,
            RingFullTimeoutMicroseconds = 2_000,
            SyntheticRecordCount = recordCount,
            SyntheticInstrumentCount = 2,
            HeartbeatIntervalMilliseconds = 5_000,
            ProducerLogicalProcessor = NativeConstants.UnpinnedProcessor,
            DrainLogicalProcessor = NativeConstants.UnpinnedProcessor,
            DatasetLength = datasetLength,
            SyntheticStartSequence = 1
        };
        return config;
    }

    internal static unsafe NativeTickerSubscription CreateSubscription(uint offset) => new()
    {
        StructSize = (uint)sizeof(NativeTickerSubscription),
        AbiVersion = NativeConstants.AbiVersion,
        SymbolOffset = offset,
        SymbolLength = 4,
        InputSymbology = 1,
        DataKinds = (uint)(MarketDataKinds.Quote
                           | MarketDataKinds.Trade
                           | MarketDataKinds.MboOrderUpdate)
    };

    internal static unsafe NativeWaitResult CreateWaitResult() => new()
    {
        StructSize = (uint)sizeof(NativeWaitResult),
        AbiVersion = NativeConstants.AbiVersion
    };

    internal static unsafe NativeBatchResult CreateBatchResult() => new()
    {
        StructSize = (uint)sizeof(NativeBatchResult),
        AbiVersion = NativeConstants.AbiVersion
    };

    internal static unsafe NativeFeedStats CreateStats() => new()
    {
        StructSize = (uint)sizeof(NativeFeedStats),
        AbiVersion = NativeConstants.AbiVersion
    };
}
