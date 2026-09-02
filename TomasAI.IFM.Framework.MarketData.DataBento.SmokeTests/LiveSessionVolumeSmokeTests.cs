namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

[Collection(DatabentoSmokeCollection.Name)]
public sealed class LiveSessionVolumeSmokeTests
{
    [Theory]
    [InlineData("ES", "GLBX.MDP3", true)]
    [InlineData("VX", "XCBF.PITCH", false)]
    public async Task CurrentFutureCompletesRequiredReplayBoundaries(
        string rootSymbol,
        string dataset,
        bool requireStatisticsReplay)
    {
        if (!LiveTestGate.IsEnabled())
            return;

        LiveTestGate.AssertCredential();
        var factory = new DatabentoFeedFactory();
        var queryOptions = LiveTestGate.CreateOptions(dataset);
        var now = LiveTestGate.UtcNowNanoseconds();
        var contract = factory.CreateMarketDataQueries(queryOptions)
            .GetContractDetails($"{rootSymbol}.FUT", TimeSpan.FromSeconds(90))
            .Where(detail => detail.ContractKind == ContractKind.Future)
            .Where(detail => detail.ExpirationTimestampNanoseconds > now)
            .OrderBy(detail => detail.ExpirationTimestampNanoseconds)
            .First();
        var replayStart = DateTimeOffset.UtcNow.AddMinutes(-5);
        var replayStartNanoseconds = checked(
            (ulong)(replayStart.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100UL);
        var options = LiveTestGate.CreateLiveOptions(dataset) with
        {
            StatisticsReplayStartTimestampNanoseconds = replayStartNanoseconds,
            TradeReplayStartTimestampNanoseconds = replayStartNanoseconds
        };

        using var feed = factory.CreateTickerFeed(options);
        feed.Subscribe(
        [
            new TickerSubscription(
                contract.RawSymbol,
                DatabentoInputSymbology.RawSymbol,
                MarketDataKinds.Quote
                | MarketDataKinds.Trade
                | MarketDataKinds.Statistics
                | MarketDataKinds.SessionVolume)
        ], TimeSpan.FromSeconds(10));
        TickerInstrumentRegistration registration = null!;
        ISynchronousBatchReader<MarketDataBatch64> reader = null!;
        feed.Start(TimeSpan.FromSeconds(45), _ =>
        {
            registration = Assert.Single(feed.GetInstruments());
            reader = feed.GetReader(registration.Instrument);
        });
        var probe = new SessionReplayProbe();
        var consumer = probe.ConsumeAsync(reader);
        try
        {
            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(45);
            while ((probe.TradeReplayCompletions == 0
                    || (requireStatisticsReplay
                        && probe.StatisticsReplayCompletions == 0))
                   && DateTimeOffset.UtcNow < deadline
                   && !consumer.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }
        finally
        {
            feed.Stop(TimeSpan.FromSeconds(30));
            await consumer.WaitAsync(TimeSpan.FromSeconds(30));
        }

        Assert.Equal(1, probe.TradeReplayCompletions);
        if (requireStatisticsReplay)
            Assert.Equal(1, probe.StatisticsReplayCompletions);
        else
            Assert.InRange(probe.StatisticsReplayCompletions, 0, 1);
        Assert.Equal(0, probe.TradesMissingReplayFlagBeforeCompletion);
        Assert.Equal(FeedState.Stopped, feed.GetHealth().State);
        Assert.Equal(DatabentoFeedStatus.Ok, feed.GetHealth().TerminalStatus);
    }

    private sealed class SessionReplayProbe
    {
        private int _tradeReplayCompletions;
        private int _statisticsReplayCompletions;
        private long _tradesMissingReplayFlagBeforeCompletion;

        internal int TradeReplayCompletions => Volatile.Read(ref _tradeReplayCompletions);
        internal int StatisticsReplayCompletions =>
            Volatile.Read(ref _statisticsReplayCompletions);
        internal long TradesMissingReplayFlagBeforeCompletion =>
            Interlocked.Read(ref _tradesMissingReplayFlagBeforeCompletion);

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

                    using (batch)
                    {
                        foreach (ref readonly var record in batch.Records)
                        {
                            switch (record.Header.RecordKind)
                            {
                                case MarketRecordKind.Trade:
                                    if ((record.Header.Flags & 2) == 0
                                        && Volatile.Read(ref _tradeReplayCompletions) == 0)
                                    {
                                        Interlocked.Increment(
                                            ref _tradesMissingReplayFlagBeforeCompletion);
                                    }
                                    break;
                                case MarketRecordKind.TradeReplayComplete:
                                    Interlocked.Increment(ref _tradeReplayCompletions);
                                    break;
                                case MarketRecordKind.StatisticsReplayComplete:
                                    Interlocked.Increment(ref _statisticsReplayCompletions);
                                    break;
                            }
                        }
                    }
                }
            });

    }
}
