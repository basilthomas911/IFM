namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class FeedLifecycleTests
{
    [Fact]
    public async Task FullManagedChannelMakesFirstStopIncompleteAndSecondStopSucceed()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            ManagedChannelRecordCapacity = 512,
            ManagedBatchRecordCapacity = 512,
            Synthetic = new SyntheticFeedOptions
            {
                RecordCount = 5_000
            }
        };
        var feed = new DatabentoFeedFactory().CreateTickerFeed(options);
        var stopped = false;
        try
        {
            feed.Subscribe(
            [
                new TickerSubscription("ESM6", DatabentoInputSymbology.RawSymbol, MarketDataKinds.Quote),
                new TickerSubscription("NQM6", DatabentoInputSymbology.RawSymbol, MarketDataKinds.Quote)
            ], TimeSpan.FromSeconds(2));
            feed.Start(TimeSpan.FromSeconds(5));

            Assert.True(SpinWait.SpinUntil(
                () => feed.GetHealth().ChannelFullCount > 0,
                TimeSpan.FromSeconds(5)));
            var incomplete = Assert.Throws<FeedStopDrainIncompleteException>(() =>
                feed.Stop(TimeSpan.FromMilliseconds(50)));
            Assert.Equal(DatabentoFeedStatus.StopDrainIncomplete, incomplete.Status);

            var registrations = feed.GetInstruments();
            var first = Task.Run(() => DrainCount(feed.GetReader(registrations[0].Instrument)));
            var second = Task.Run(() => DrainCount(feed.GetReader(registrations[1].Instrument)));
            Assert.Equal(2_500, await first.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(2_500, await second.WaitAsync(TimeSpan.FromSeconds(10)));

            feed.Stop(TimeSpan.FromSeconds(5));
            stopped = true;
            Assert.True(feed.GetHealth().ChannelFullCount > 0);
        }
        finally
        {
            if (!stopped)
            {
                try
                {
                    feed.Stop(TimeSpan.FromSeconds(5));
                    stopped = true;
                }
                catch
                {
                }
            }
            if (stopped)
            {
                feed.Dispose();
            }
        }
    }

    [Fact]
    public void RepeatedSyntheticLifecycleCompletesWithoutOwnershipFailures()
    {
        for (var cycle = 0; cycle < 25; cycle++)
        {
            var options = DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.SyntheticCi,
                "SYNTHETIC") with
            {
                Synthetic = new SyntheticFeedOptions
                {
                    RecordCount = 256,
                    StartSequence = checked((ulong)(cycle * 256 + 1))
                }
            };
            var feed = new DatabentoFeedFactory().CreateTickerFeed(options);
            var stopped = false;
            try
            {
                feed.Subscribe(
                [
                    new TickerSubscription(
                        "ESM6",
                        DatabentoInputSymbology.RawSymbol,
                        MarketDataKinds.Quote | MarketDataKinds.Trade)
                ], TimeSpan.FromSeconds(1));
                feed.Start(TimeSpan.FromSeconds(2));
                Assert.Equal(256, DrainCount(feed.GetReader(feed.GetInstruments()[0].Instrument)));
                feed.Stop(TimeSpan.FromSeconds(2));
                stopped = true;
                Assert.Equal(256ul, feed.GetHealth().RecordsConsumed);
            }
            finally
            {
                if (!stopped)
                {
                    try
                    {
                        feed.Stop(TimeSpan.FromSeconds(2));
                        stopped = true;
                    }
                    catch
                    {
                    }
                }
                if (stopped)
                {
                    feed.Dispose();
                }
            }
        }
    }

    [Fact]
    public void AutomaticTopologyResolvesDistinctProcessorLocationsWhenAvailable()
    {
        if (Environment.ProcessorCount < 2)
        {
            return;
        }
        var pair = ProcessorTopology.ResolvePair(requirePerformanceCore: false);
        Assert.NotEqual(pair.NativeProducer, pair.ManagedDrain);
    }

    [Fact]
    public void UncommittedPlacementLeaseReleasesItsProcessorReservations()
    {
        if (Environment.ProcessorCount < 2)
        {
            return;
        }
        var pair = ProcessorTopology.ResolvePair(requirePerformanceCore: false);
        var affinity = new FeedCpuAffinityOptions
        {
            Mode = CpuAffinityMode.Explicit,
            NativeProducer = pair.NativeProducer,
            ManagedDrain = pair.ManagedDrain,
            RequirePerformanceCore = false
        };
        var isolation = new FeedCoreIsolationOptions
        {
            Mode = FeedCoreIsolationMode.PinnedOnly
        };
        var numa = new FeedNumaOptions
        {
            Mode = NumaLocalityMode.Disabled
        };

        using (ProcessCoreIsolationCoordinator.Acquire(
                   affinity, isolation, numa, new FeedProcessorResidencyOptions()))
        {
        }
        using var reacquired = ProcessCoreIsolationCoordinator.Acquire(
            affinity,
            isolation,
            numa,
            new FeedProcessorResidencyOptions());
    }

    private static int DrainCount(ISynchronousBatchReader<MarketDataBatch64> reader)
    {
        var count = 0;
        while (true)
        {
            try
            {
                using var batch = reader.Read(TimeSpan.FromSeconds(5));
                count += batch.Count;
            }
            catch (EndOfStreamException)
            {
                return count;
            }
        }
    }
}
