using System.Diagnostics;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;
using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

[Collection(DatabentoSmokeCollection.Name)]
[Trait("Category", "Manual")]
[Trait("Category", "LongRunning")]
public sealed class DatabentoOneHourLiveSmokeTests
{
    private readonly DatabentoSmokeFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DatabentoOneHourLiveSmokeTests(
        DatabentoSmokeFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task CurrentEsFutureReceivesEveryTickForConfiguredDuration()
    {
        if (!LiveTestGate.IsOneHourTestEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var duration = LiveTestGate.GetSoakDuration();
        var dataKinds = LiveTestGate.GetSoakDataKinds();
        var currentFuture = FindCurrentEsFuture(_fixture.Queries);
        var options = LiveTestGate.CreateLiveOptions();

        using var feed = new DatabentoFeedFactory().CreateTickerFeed(options);
        feed.Subscribe(
        [
            new TickerSubscription(
                currentFuture.RawSymbol,
                DatabentoInputSymbology.RawSymbol,
                dataKinds)
        ], TimeSpan.FromSeconds(10));
        feed.Start(TimeSpan.FromSeconds(60));
        var registration = Assert.Single(feed.GetInstruments());
        var counters = new DatabentoSoakCounters([registration.Instrument]);
        var consumer = counters.ConsumeAsync(feed.GetReader(registration.Instrument));

        _output.WriteLine(
            "Starting ES future soak: symbol={0}, instrument={1}, kinds={2}, duration={3}.",
            registration.RawSymbol,
            registration.Instrument,
            dataKinds,
            duration);
        await RunAndStopAsync(duration, feed.GetHealth, feed.Stop, consumer, counters);

        var health = feed.GetHealth();
        WriteFinalSummary("ES future", duration, counters, health);
        AssertSuccessfulSoak(duration, counters, health);
    }

    [Fact]
    public async Task CurrentEsFutureOptionsReceiveEveryTickForConfiguredDuration()
    {
        if (!LiveTestGate.IsOneHourTestEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var duration = LiveTestGate.GetSoakDuration();
        var dataKinds = LiveTestGate.GetSoakDataKinds();
        var maturity = FindNearestEsOptionMaturity(_fixture.Queries);
        var definitions = _fixture.Queries.GetChainDefinitions(
            new OptionChainDefinitionRequest
            {
                Dataset = "GLBX.MDP3",
                Underlying = "ES",
                MaturityDate = maturity,
                UniversePolicy = OptionUniversePolicy.ParentOptionSymbol,
                Rights = OptionRightSelection.Both
            },
            TimeSpan.FromSeconds(180));
        var contracts = definitions.Contracts
            .Where(contract => !string.IsNullOrWhiteSpace(contract.Underlying))
            .GroupBy(contract => contract.Underlying, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .First()
            .OrderBy(contract => contract.StrikePrice)
            .ThenBy(contract => contract.Right)
            .ToArray();
        Assert.Contains(contracts, contract => contract.Right == OptionRightSelection.Call);
        Assert.Contains(contracts, contract => contract.Right == OptionRightSelection.Put);

        using var feed = new DatabentoFeedFactory().CreateOptionChainFeed(
            LiveTestGate.CreateLiveOptions());
        feed.Subscribe(
            new OptionChainSubscription
            {
                Underlying = contracts[0].Underlying,
                MaturityDate = maturity,
                Strikes = contracts.Select(contract => contract.StrikePrice).Distinct().ToArray(),
                Rights = OptionRightSelection.Both,
                ResolvedContracts = contracts,
                DataKinds = dataKinds
            },
            TimeSpan.FromSeconds(15));
        feed.Start(TimeSpan.FromSeconds(90));
        var expectedInstruments = contracts.Select(contract => contract.Instrument).ToHashSet();
        var counters = new DatabentoSoakCounters(expectedInstruments);
        var consumer = counters.ConsumeAsync(feed.Reader);

        _output.WriteLine(
            "Starting ES option soak: underlying={0}, maturity={1:yyyy-MM-dd}, "
            + "contracts={2}, kinds={3}, duration={4}.",
            contracts[0].Underlying,
            maturity,
            contracts.Length,
            dataKinds,
            duration);
        await RunAndStopAsync(duration, feed.GetHealth, feed.Stop, consumer, counters);

        var health = feed.GetHealth();
        WriteFinalSummary("ES futures options", duration, counters, health);
        AssertSuccessfulSoak(duration, counters, health);
    }

    private async Task RunAndStopAsync(
        TimeSpan duration,
        Func<FeedHealthSnapshot> getHealth,
        Action<TimeSpan> stop,
        Task consumer,
        DatabentoSoakCounters counters)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            while (Stopwatch.GetElapsedTime(started) < duration)
            {
                var remaining = duration - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }
                await Task.Delay(remaining < TimeSpan.FromMinutes(1)
                    ? remaining
                    : TimeSpan.FromMinutes(1));
                var elapsed = Stopwatch.GetElapsedTime(started);
                var health = getHealth();
                WriteProgress(elapsed, counters, health);
                if (consumer.IsCompleted && elapsed < duration)
                {
                    throw new InvalidOperationException(
                        "The market-data consumer completed before the soak duration elapsed.");
                }
                if (health.State != FeedState.Running
                    || health.TerminalStatus != DatabentoFeedStatus.Ok)
                {
                    throw new DatabentoFeedException(
                        health.TerminalStatus,
                        $"The feed entered {health.State} during the soak test.");
                }
            }
        }
        catch (Exception exception)
        {
            counters.RecordException("monitor", exception);
        }
        finally
        {
            try
            {
                stop(TimeSpan.FromSeconds(60));
            }
            catch (Exception exception)
            {
                counters.RecordException("stop", exception);
            }
            try
            {
                await consumer.WaitAsync(TimeSpan.FromSeconds(60));
            }
            catch (Exception exception)
            {
                counters.RecordException("consumer completion", exception);
            }
        }
    }

    private void WriteProgress(
        TimeSpan elapsed,
        DatabentoSoakCounters counters,
        FeedHealthSnapshot health)
    {
        var rate = elapsed.TotalSeconds <= 0
            ? 0
            : counters.Ticks / elapsed.TotalSeconds;
        _output.WriteLine(
            "[{0:hh\\:mm\\:ss}] ticks={1:N0}, rate={2:N0}/s, quote={3:N0}, "
            + "trade={4:N0}, mbo={5:N0}, exceptions={6:N0}, state={7}, "
            + "ring={8:N0}/{9:N0}, channelFull={10:N0}, poolMiss={11:N0}.",
            elapsed,
            counters.Ticks,
            rate,
            counters.Quotes,
            counters.Trades,
            counters.MboUpdates,
            counters.Exceptions,
            health.State,
            health.RingUsedRecords,
            health.RingCapacityRecords,
            health.ChannelFullCount,
            health.PoolMissCount);
    }

    private void WriteFinalSummary(
        string testName,
        TimeSpan duration,
        DatabentoSoakCounters counters,
        FeedHealthSnapshot health)
    {
        _output.WriteLine(
            "FINAL {0}: duration={1}, ticks={2:N0}, batches={3:N0}, quote={4:N0}, "
            + "trade={5:N0}, mbo={6:N0}, instrumentsWithTicks={7:N0}/{8:N0}, "
            + "exceptions={9:N0}, produced={10:N0}, consumed={11:N0}, "
            + "channelFull={12:N0}, poolMiss={13:N0}, warning={14}.",
            testName,
            duration,
            counters.Ticks,
            counters.Batches,
            counters.Quotes,
            counters.Trades,
            counters.MboUpdates,
            counters.InstrumentsWithTicks,
            counters.ExpectedInstrumentCount,
            counters.Exceptions,
            health.RecordsProduced,
            health.RecordsConsumed,
            health.ChannelFullCount,
            health.PoolMissCount,
            health.Warning ?? "none");
        foreach (var message in counters.ExceptionMessages)
        {
            _output.WriteLine("EXCEPTION: {0}", message);
        }
        foreach (var pair in counters.GetInstrumentCounts().Take(20))
        {
            _output.WriteLine(
                "INSTRUMENT: publisher={0}, instrument={1}, ticks={2:N0}.",
                pair.Key.PublisherId,
                pair.Key.InstrumentId,
                pair.Value);
        }
    }

    private static void AssertSuccessfulSoak(
        TimeSpan duration,
        DatabentoSoakCounters counters,
        FeedHealthSnapshot health)
    {
        var failures = new List<string>();
        if (counters.Ticks == 0)
        {
            failures.Add($"No ticks were received during {duration}.");
        }
        if (counters.Exceptions != 0)
        {
            failures.Add($"The exception counter is {counters.Exceptions}, expected zero.");
        }
        if (counters.UnknownInstruments != 0)
        {
            failures.Add(
                $"Received {counters.UnknownInstruments} ticks for unknown instruments.");
        }
        if (counters.UnexpectedRecordKinds != 0)
        {
            failures.Add(
                $"Received {counters.UnexpectedRecordKinds} ticks with unknown record kinds.");
        }
        if (health.State != FeedState.Stopped)
        {
            failures.Add($"Final feed state is {health.State}, expected Stopped.");
        }
        if (health.TerminalStatus != DatabentoFeedStatus.Ok)
        {
            failures.Add(
                $"Final terminal status is {health.TerminalStatus}, expected Ok.");
        }
        if (health.RingUsedRecords != 0)
        {
            failures.Add(
                $"The native ring still contains {health.RingUsedRecords} records.");
        }
        if (health.ChannelBatchCount != 0)
        {
            failures.Add(
                $"Managed channels still contain {health.ChannelBatchCount} batches.");
        }
        if (health.RecordsProduced != health.RecordsConsumed)
        {
            failures.Add(
                $"Produced {health.RecordsProduced} records but the managed drain consumed "
                + $"{health.RecordsConsumed}.");
        }
        if (checked((ulong)counters.Ticks) != health.RecordsConsumed)
        {
            failures.Add(
                $"The test consumed {counters.Ticks} ticks but feed health reports "
                + $"{health.RecordsConsumed} consumed records.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static ContractDetail FindCurrentEsFuture(IDatabentoMarketDataQueries queries)
    {
        var now = LiveTestGate.UtcNowNanoseconds();
        return queries.GetContractDetails("ES.FUT", TimeSpan.FromSeconds(90))
            .Where(detail => detail.ContractKind == ContractKind.Future)
            .Where(detail => detail.ExpirationTimestampNanoseconds > now)
            .OrderBy(detail => detail.ExpirationTimestampNanoseconds)
            .First();
    }

    private static DateOnly FindNearestEsOptionMaturity(
        IDatabentoMarketDataQueries queries)
    {
        var now = LiveTestGate.UtcNowNanoseconds();
        return queries.GetContractDetails("ES.OPT", TimeSpan.FromSeconds(180))
            .Where(detail => detail.ContractKind is
                ContractKind.CallOption or ContractKind.PutOption)
            .Where(detail => detail.ExpirationTimestampNanoseconds > now)
            .Where(detail => detail.MaturityDate is not null)
            .Select(detail => detail.MaturityDate!.Value)
            .Min();
    }
}
