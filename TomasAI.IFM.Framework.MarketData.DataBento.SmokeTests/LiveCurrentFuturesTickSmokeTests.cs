using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

[Collection(DatabentoSmokeCollection.Name)]
public sealed class LiveCurrentFuturesTickSmokeTests(ITestOutputHelper output)
{
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(45);

    [Fact]
    public Task CurrentEsFutureReceivesLiveQuotesAndTrades() =>
        VerifyCurrentFutureAsync("ES", "GLBX.MDP3");

    [Fact]
    public Task CurrentVxFutureReceivesLiveQuotesAndTrades() =>
        VerifyCurrentFutureAsync("VX", "XCBF.PITCH");

    private async Task VerifyCurrentFutureAsync(string rootSymbol, string dataset)
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }

        LiveTestGate.AssertCredential();
        var queryOptions = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            dataset);
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(queryOptions);
        var now = LiveTestGate.UtcNowNanoseconds();
        var currentFuture = queries
            .GetContractDetails($"{rootSymbol}.FUT", TimeSpan.FromSeconds(90))
            .Where(detail => detail.ContractKind == ContractKind.Future)
            .Where(detail => detail.ExpirationTimestampNanoseconds > now)
            .OrderBy(detail => detail.ExpirationTimestampNanoseconds)
            .First();
        var liveOptions = queryOptions with
        {
            DataSource = FeedDataSourceMode.DatabentoLive,
            CpuAffinity = new FeedCpuAffinityOptions
            {
                Mode = CpuAffinityMode.Unpinned,
                RequirePerformanceCore = false
            },
            ThreadPriority = new FeedThreadPriorityOptions(),
            Memory = new FeedMemoryOptions { LockRingMemory = false },
            GarbageCollection = new FeedGcOptions
            {
                EnableSustainedLowLatency = false
            },
            Numa = new FeedNumaOptions { Mode = NumaLocalityMode.Disabled },
            CoreIsolation = new FeedCoreIsolationOptions
            {
                Mode = FeedCoreIsolationMode.PinnedOnly
            }
        };

        var feed = new DatabentoFeedFactory().CreateTickerFeed(liveOptions);
        feed.Subscribe(
        [
            new TickerSubscription(
                currentFuture.RawSymbol,
                DatabentoInputSymbology.RawSymbol,
                MarketDataKinds.Quote | MarketDataKinds.Trade)
        ], TimeSpan.FromSeconds(10));
        feed.Start(TimeSpan.FromSeconds(45));

        var registration = Assert.Single(feed.GetInstruments());
        var counters = new DatabentoSoakCounters(
            [registration.Instrument],
            allowPublisherAliases: true);
        var consumer = counters.ConsumeAsync(feed.GetReader(registration.Instrument));
        Exception? stopFailure = null;
        try
        {
            var deadline = DateTimeOffset.UtcNow + ObservationTimeout;
            while ((counters.Quotes == 0 || counters.Trades == 0)
                   && DateTimeOffset.UtcNow < deadline
                   && !consumer.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }
        finally
        {
            try
            {
                feed.Stop(TimeSpan.FromSeconds(30));
                await consumer.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (Exception exception)
            {
                stopFailure = exception;
            }
        }

        var health = feed.GetHealth();
        output.WriteLine(
            "LIVE {0}: dataset={1}, rawSymbol={2}, publisher={3}, instrument={4}, quotes={5}, trades={6}, state={7}, terminalStatus={8}.",
            rootSymbol,
            dataset,
            registration.RawSymbol,
            registration.Instrument.PublisherId,
            registration.Instrument.InstrumentId,
            counters.Quotes,
            counters.Trades,
            health.State,
            health.TerminalStatus);

        if (stopFailure is not null)
        {
            output.WriteLine(
                "LIVE {0} stop failure: {1}: {2}",
                rootSymbol,
                stopFailure.GetType().Name,
                stopFailure.Message);
            throw stopFailure;
        }

        try
        {
            Assert.True(
                counters.Quotes > 0,
                $"{dataset}/{currentFuture.RawSymbol} returned no live quote ticks in {ObservationTimeout}.");
            Assert.True(
                counters.Trades > 0,
                $"{dataset}/{currentFuture.RawSymbol} returned no live trade ticks in {ObservationTimeout}.");
            Assert.Equal(0, counters.UnknownInstruments);
            Assert.Equal(0, counters.UnexpectedRecordKinds);
            Assert.Equal(0, counters.Exceptions);
            Assert.Equal(FeedState.Stopped, health.State);
            Assert.Equal(DatabentoFeedStatus.Ok, health.TerminalStatus);
        }
        finally
        {
            feed.Dispose();
        }
    }
}
