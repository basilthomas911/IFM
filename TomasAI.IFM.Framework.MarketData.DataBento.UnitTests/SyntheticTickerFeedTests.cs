using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class SyntheticTickerFeedTests
{
    [Fact]
    public void IdenticalSubscriptionsCoalesceAndConflictingSubscriptionsFail()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC");
        var subscription = new TickerSubscription(
            "ESM6",
            DatabentoInputSymbology.RawSymbol,
            MarketDataKinds.Quote);
        using (var coalesced = new DatabentoFeedFactory().CreateTickerFeed(options))
        {
            coalesced.Subscribe([subscription, subscription], TimeSpan.FromSeconds(1));
        }

        using var conflicting = new DatabentoFeedFactory().CreateTickerFeed(options);
        Assert.Throws<ArgumentException>(() => conflicting.Subscribe(
        [
            subscription,
            subscription with { DataKinds = MarketDataKinds.Trade }
        ], TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task TickerFeedPublishesOrderedBatchesPerInstrumentWithoutDrainAllocations()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            Synthetic = new SyntheticFeedOptions
            {
                RecordCount = 8_000
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
                    MarketDataKinds.Quote | MarketDataKinds.Trade | MarketDataKinds.MboOrderUpdate),
                new TickerSubscription(
                    "NQM6",
                    DatabentoInputSymbology.RawSymbol,
                    MarketDataKinds.Quote | MarketDataKinds.Trade | MarketDataKinds.MboOrderUpdate)
            ], TimeSpan.FromSeconds(2));

            feed.Start(TimeSpan.FromSeconds(5));
            var registrations = feed.GetInstruments();
            Assert.Equal(2, registrations.Count);
            var firstTask = Task.Run(() =>
                Drain(feed.GetReader(registrations[0].Instrument), expectedIncrement: 2));
            var secondTask = Task.Run(() =>
                Drain(feed.GetReader(registrations[1].Instrument), expectedIncrement: 2));
            var first = await firstTask.WaitAsync(TimeSpan.FromSeconds(10));
            var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(4_000, first.Count);
            Assert.Equal(4_000, second.Count);
            Assert.Equal(8_000, first.Count + second.Count);

            feed.Stop(TimeSpan.FromSeconds(5));
            stopped = true;
            var health = feed.GetHealth();
            Assert.Equal(8_000ul, health.RecordsProduced);
            Assert.Equal(8_000ul, health.RecordsConsumed);
            Assert.Equal(0, health.DrainAllocatedBytes);
            Assert.Null(health.Warning);
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
    public void OptionChainUsesOneSharedReaderAndPreservesSessionOrder()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC") with
        {
            Synthetic = new SyntheticFeedOptions
            {
                RecordCount = 2_000
            }
        };
        using var feed = new DatabentoFeedFactory().CreateOptionChainFeed(options);
        feed.Subscribe(new OptionChainSubscription
        {
            Underlying = "ESM6",
            MaturityDate = new DateOnly(2026, 6, 19),
            ResolvedContracts =
            [
                new OptionContractSelection("ESM6 C5000", new InstrumentKey(1, 1)),
                new OptionContractSelection("ESM6 P5000", new InstrumentKey(1, 2))
            ],
            DataKinds = MarketDataKinds.Quote | MarketDataKinds.Trade
        }, TimeSpan.FromSeconds(2));

        feed.Start(TimeSpan.FromSeconds(5));
        var records = Drain(feed.Reader, expectedIncrement: 1);
        Assert.Equal(2_000, records.Count);
        feed.Stop(TimeSpan.FromSeconds(5));
        Assert.Equal(0, feed.GetHealth().DrainAllocatedBytes);
    }

    private static List<uint> Drain(
        ISynchronousBatchReader<MarketDataBatch64> reader,
        uint expectedIncrement)
    {
        var result = new List<uint>();
        while (true)
        {
            MarketDataBatch64 batch;
            try
            {
                batch = reader.Read(TimeSpan.FromSeconds(5));
            }
            catch (EndOfStreamException)
            {
                break;
            }
            using (batch)
            {
                foreach (var record in batch.Records)
                {
                    if (result.Count != 0)
                    {
                        Assert.Equal(result[^1] + expectedIncrement, record.Header.Sequence);
                    }
                    result.Add(record.Header.Sequence);
                }
            }
        }
        return result;
    }
}
