using System.Diagnostics;
using System.Text.Json;
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
        var scheduledStart = LiveTestGate.GetSoakStart();
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
        using var csvCapture = DatabentoTickCsvCapture.CreateIfEnabled(
            "es-future",
            new Dictionary<InstrumentKey, string>
            {
                [registration.Instrument] = registration.RawSymbol
            });
        var counters = new DatabentoSoakCounters(
            [registration.Instrument],
            csvCapture);
        if (scheduledStart is not null)
        {
            counters.PauseMeasurement();
        }
        var consumer = counters.ConsumeAsync(feed.GetReader(registration.Instrument));

        _output.WriteLine(
            "Starting ES future soak: symbol={0}, instrument={1}, kinds={2}, duration={3}.",
            registration.RawSymbol,
            registration.Instrument,
            dataKinds,
            duration);
        WriteCsvCaptureStatus(counters);
        var runtime = await RunAndStopAsync(
            duration,
            feed.GetHealth,
            feed.Stop,
            consumer,
            counters,
            scheduledStart);

        var health = feed.GetHealth();
        WriteFinalSummary("ES future", duration, counters, health, runtime);
        WriteMachineReadableResult("es-future", duration, counters, health, runtime);
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
        var scheduledStart = LiveTestGate.GetSoakStart();
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
        using var csvCapture = DatabentoTickCsvCapture.CreateIfEnabled(
            "es-futures-options",
            contracts
                .GroupBy(contract => contract.Instrument)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().RawSymbol));
        var counters = new DatabentoSoakCounters(expectedInstruments, csvCapture);
        if (scheduledStart is not null)
        {
            counters.PauseMeasurement();
        }
        var consumer = counters.ConsumeAsync(feed.Reader);

        _output.WriteLine(
            "Starting ES option soak: underlying={0}, maturity={1:yyyy-MM-dd}, "
            + "contracts={2}, kinds={3}, duration={4}.",
            contracts[0].Underlying,
            maturity,
            contracts.Length,
            dataKinds,
            duration);
        WriteCsvCaptureStatus(counters);
        var runtime = await RunAndStopAsync(
            duration,
            feed.GetHealth,
            feed.Stop,
            consumer,
            counters,
            scheduledStart);

        var health = feed.GetHealth();
        WriteFinalSummary("ES futures options", duration, counters, health, runtime);
        WriteMachineReadableResult(
            "es-futures-options",
            duration,
            counters,
            health,
            runtime);
        AssertSuccessfulSoak(duration, counters, health);
    }

    private async Task<SoakRuntimeMetrics> RunAndStopAsync(
        TimeSpan duration,
        Func<FeedHealthSnapshot> getHealth,
        Action<TimeSpan> stop,
        Task consumer,
        DatabentoSoakCounters counters,
        DateTimeOffset? scheduledStart)
    {
        if (scheduledStart is not null && scheduledStart > DateTimeOffset.Now)
        {
            _output.WriteLine(
                "Feed warm-up active; measurement starts at {0:o}.",
                scheduledStart);
            while (DateTimeOffset.Now < scheduledStart)
            {
                var remaining = scheduledStart.Value - DateTimeOffset.Now;
                await Task.Delay(remaining < TimeSpan.FromMinutes(1)
                    ? remaining
                    : TimeSpan.FromMinutes(1));
                var health = getHealth();
                if (consumer.IsCompleted
                    || health.State != FeedState.Running
                    || health.TerminalStatus != DatabentoFeedStatus.Ok)
                {
                    throw new DatabentoFeedException(
                        health.TerminalStatus,
                        $"The feed failed during warm-up with state {health.State}.");
                }
            }
        }
        var startingHealth = getHealth();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var startedOn = DateTimeOffset.UtcNow;
        var startingCpu = process.TotalProcessorTime;
        var startingAllocatedBytes = GC.GetTotalAllocatedBytes(false);
        var startingGen0 = GC.CollectionCount(0);
        var startingGen1 = GC.CollectionCount(1);
        var startingGen2 = GC.CollectionCount(2);
        var started = Stopwatch.GetTimestamp();
        counters.BeginMeasurement();
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
            try
            {
                counters.FlushCapture();
            }
            catch (Exception exception)
            {
                counters.RecordException("CSV flush", exception);
            }
        }

        var actualElapsed = Stopwatch.GetElapsedTime(started);
        process.Refresh();
        return new SoakRuntimeMetrics(
            startedOn,
            DateTimeOffset.UtcNow,
            actualElapsed,
            process.TotalProcessorTime - startingCpu,
            process.WorkingSet64,
            process.PeakWorkingSet64,
            process.PrivateMemorySize64,
            GC.GetTotalMemory(false),
            GC.GetTotalAllocatedBytes(false) - startingAllocatedBytes,
            GC.CollectionCount(0) - startingGen0,
            GC.CollectionCount(1) - startingGen1,
            GC.CollectionCount(2) - startingGen2,
            startingHealth);
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
            + "ring={8:N0}/{9:N0}, channelFull={10:N0}, poolMiss={11:N0}, "
            + "csvRows={12:N0}.",
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
            health.PoolMissCount,
            counters.CapturedCsvRows);
    }

    private void WriteFinalSummary(
        string testName,
        TimeSpan duration,
        DatabentoSoakCounters counters,
        FeedHealthSnapshot health,
        SoakRuntimeMetrics runtime)
    {
        var ticksPerSecond = runtime.Elapsed.TotalSeconds <= 0
            ? 0
            : counters.Ticks / runtime.Elapsed.TotalSeconds;
        var averageCpuCores = runtime.Elapsed.TotalSeconds <= 0
            ? 0
            : runtime.CpuTime.TotalSeconds / runtime.Elapsed.TotalSeconds;
        var nativeBatches = Difference(
            health.BatchesPublished,
            runtime.StartingHealth.BatchesPublished);
        var channelFull = Difference(
            health.ChannelFullCount,
            runtime.StartingHealth.ChannelFullCount);
        var poolMiss = Difference(
            health.PoolMissCount,
            runtime.StartingHealth.PoolMissCount);
        var drainAllocatedBytes = Difference(
            health.DrainAllocatedBytes,
            runtime.StartingHealth.DrainAllocatedBytes);
        _output.WriteLine(
            "FINAL {0}: duration={1}, ticks={2:N0}, batches={3:N0}, quote={4:N0}, "
            + "trade={5:N0}, mbo={6:N0}, instrumentsWithTicks={7:N0}/{8:N0}, "
            + "exceptions={9:N0}, produced={10:N0}, consumed={11:N0}, "
            + "ringHighWater={12:N0}, nativeBatches={13:N0}, channelFull={14:N0}, "
            + "poolMiss={15:N0}, drainAllocatedBytes={16:N0}, warning={17}, "
            + "ticksPerSecond={18:N2}, cpuSeconds={19:N2}, averageCpuCores={20:N4}, "
            + "workingSetBytes={21:N0}, peakWorkingSetBytes={22:N0}, "
            + "privateBytes={23:N0}, managedHeapBytes={24:N0}, "
            + "managedAllocatedBytes={25:N0}, gen0={26:N0}, gen1={27:N0}, gen2={28:N0}, "
            + "csvRows={29:N0}, csvBytes={30:N0}, csvPath={31}.",
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
            health.RingHighWaterRecords,
            nativeBatches,
            channelFull,
            poolMiss,
            drainAllocatedBytes,
            health.Warning ?? "none",
            ticksPerSecond,
            runtime.CpuTime.TotalSeconds,
            averageCpuCores,
            runtime.WorkingSetBytes,
            runtime.PeakWorkingSetBytes,
            runtime.PrivateMemoryBytes,
            runtime.ManagedHeapBytes,
            runtime.ManagedAllocatedBytes,
            runtime.Gen0Collections,
            runtime.Gen1Collections,
            runtime.Gen2Collections,
            counters.CapturedCsvRows,
            counters.CapturedCsvBytes,
            counters.CapturedCsvPath ?? "disabled");
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

    private static void WriteMachineReadableResult(
        string scenario,
        TimeSpan configuredDuration,
        DatabentoSoakCounters counters,
        FeedHealthSnapshot health,
        SoakRuntimeMetrics runtime)
    {
        var resultPath = Environment.GetEnvironmentVariable(
            "IFM_DATABENTO_SOAK_RESULT_PATH");
        if (string.IsNullOrWhiteSpace(resultPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(resultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var result = new
        {
            schemaVersion = 1,
            implementation = Environment.GetEnvironmentVariable(
                "IFM_DATABENTO_NATIVE_IMPLEMENTATION") ?? "unspecified",
            scenario,
            configuredDurationSeconds = configuredDuration.TotalSeconds,
            startedOnUtc = runtime.StartedOn,
            completedOnUtc = runtime.CompletedOn,
            elapsedSeconds = runtime.Elapsed.TotalSeconds,
            ticks = counters.Ticks,
            lifetimeTicks = counters.LifetimeTicks,
            ticksPerSecond = runtime.Elapsed.TotalSeconds <= 0
                ? 0
                : counters.Ticks / runtime.Elapsed.TotalSeconds,
            batches = counters.Batches,
            lifetimeBatches = counters.LifetimeBatches,
            quotes = counters.Quotes,
            trades = counters.Trades,
            mboUpdates = counters.MboUpdates,
            expectedInstruments = counters.ExpectedInstrumentCount,
            instrumentsWithTicks = counters.InstrumentsWithTicks,
            unknownInstruments = counters.UnknownInstruments,
            unexpectedRecordKinds = counters.UnexpectedRecordKinds,
            exceptions = counters.Exceptions,
            native = new
            {
                state = health.State.ToString(),
                terminalStatus = health.TerminalStatus.ToString(),
                health.RingCapacityRecords,
                health.RingUsedRecords,
                health.RingHighWaterRecords,
                health.RecordsProduced,
                health.RecordsConsumed,
                measurementStartRecordsProduced =
                    runtime.StartingHealth.RecordsProduced,
                measurementStartRecordsConsumed =
                    runtime.StartingHealth.RecordsConsumed,
                recordsProducedDuringMeasurement = Difference(
                    health.RecordsProduced,
                    runtime.StartingHealth.RecordsProduced),
                recordsConsumedDuringMeasurement = Difference(
                    health.RecordsConsumed,
                    runtime.StartingHealth.RecordsConsumed),
                batchesPublishedDuringMeasurement = Difference(
                    health.BatchesPublished,
                    runtime.StartingHealth.BatchesPublished),
                channelFullCount = Difference(
                    health.ChannelFullCount,
                    runtime.StartingHealth.ChannelFullCount),
                poolMissCount = Difference(
                    health.PoolMissCount,
                    runtime.StartingHealth.PoolMissCount),
                drainAllocatedBytes = Difference(
                    health.DrainAllocatedBytes,
                    runtime.StartingHealth.DrainAllocatedBytes),
                health.ChannelBatchCapacity,
                health.ChannelBatchCount,
                health.PoolBatchCapacity,
                health.PoolFreeBatchCount,
                health.DrainPassLimitHitCount,
                ringHighWaterPercent = health.RingCapacityRecords == 0
                    ? 0
                    : 100d * health.RingHighWaterRecords / health.RingCapacityRecords,
                maximumChannelFullWaitMilliseconds =
                    health.MaximumChannelFullWait.TotalMilliseconds,
                health.Warning
            },
            process = new
            {
                cpuSeconds = runtime.CpuTime.TotalSeconds,
                averageCpuCores = runtime.Elapsed.TotalSeconds <= 0
                    ? 0
                    : runtime.CpuTime.TotalSeconds / runtime.Elapsed.TotalSeconds,
                cpuSecondsPerMillionTicks = counters.Ticks <= 0
                    ? 0
                    : runtime.CpuTime.TotalSeconds * 1_000_000d / counters.Ticks,
                runtime.WorkingSetBytes,
                runtime.PeakWorkingSetBytes,
                runtime.PrivateMemoryBytes,
                runtime.ManagedHeapBytes,
                runtime.ManagedAllocatedBytes,
                managedAllocatedBytesPerMillionTicks = counters.Ticks <= 0
                    ? 0
                    : runtime.ManagedAllocatedBytes * 1_000_000d / counters.Ticks,
                runtime.Gen0Collections,
                runtime.Gen1Collections,
                runtime.Gen2Collections
            },
            csv = new
            {
                rows = counters.CapturedCsvRows,
                bytes = counters.CapturedCsvBytes,
                path = counters.CapturedCsvPath
            },
            exceptionMessages = counters.ExceptionMessages
        };
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }));
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
                $"Produced {health.RecordsProduced} lifetime records but the managed drain "
                + $"consumed {health.RecordsConsumed}.");
        }
        if (checked((ulong)counters.LifetimeTicks) != health.RecordsConsumed)
        {
            failures.Add(
                $"The test consumed {counters.LifetimeTicks} lifetime ticks but feed health "
                + $"reports {health.RecordsConsumed} consumed records.");
        }
        if (counters.CapturedCsvPath is not null
            && counters.CapturedCsvRows != counters.Ticks)
        {
            failures.Add(
                $"CSV capture wrote {counters.CapturedCsvRows} rows but the test consumed "
                + $"{counters.Ticks} ticks.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private void WriteCsvCaptureStatus(DatabentoSoakCounters counters)
    {
        _output.WriteLine(
            counters.CapturedCsvPath is null
                ? "CSV capture: disabled."
                : $"CSV capture: enabled, path={counters.CapturedCsvPath}.");
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

    private static ulong Difference(ulong finalValue, ulong startingValue) =>
        finalValue >= startingValue ? finalValue - startingValue : 0;

    private static long Difference(long finalValue, long startingValue) =>
        finalValue >= startingValue ? finalValue - startingValue : 0;

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

    private sealed record SoakRuntimeMetrics(
        DateTimeOffset StartedOn,
        DateTimeOffset CompletedOn,
        TimeSpan Elapsed,
        TimeSpan CpuTime,
        long WorkingSetBytes,
        long PeakWorkingSetBytes,
        long PrivateMemoryBytes,
        long ManagedHeapBytes,
        long ManagedAllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections,
        FeedHealthSnapshot StartingHealth);
}
