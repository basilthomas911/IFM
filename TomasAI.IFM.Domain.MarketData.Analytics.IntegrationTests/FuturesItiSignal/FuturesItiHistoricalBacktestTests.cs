using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

/// <summary>Explicit opt-in; ordinary integration suites never consume historical data.</summary>
public sealed class HistoricalBacktestFactAttribute : FactAttribute
{
    public HistoricalBacktestFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_BACKTEST_POSTGRES_CONNECTION") is null)
            Skip = "Run scripts/Backtest/Run-ItiBacktest.ps1 to select isolated backtest databases.";
    }
}

[Trait("Category", "Integration")]
[Trait("Purpose", "Backtest")]
[Collection(ItiPipelineIntegrationCollection.Name)]
public sealed class FuturesItiHistoricalBacktestTests(
    TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> factory,
    MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>,
      IClassFixture<MarketDataAnalyticsFixture>
{
    [HistoricalBacktestFact]
    public async Task HistoricalEsVxTicks_ProduceDurableDailyWeeklyMonthlySignals()
    {
        var inputPath = Environment.GetEnvironmentVariable("IFM_BACKTEST_INPUT")
            ?? throw new InvalidOperationException("IFM_BACKTEST_INPUT is required.");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(inputPath));
        var input = document.RootElement;
        input.GetProperty("keyspace").GetString().Should().Be("trade_tick_quote_backtest_db");
        if (Environment.GetEnvironmentVariable("IFM_BACKTEST_DAILY_COMPUTE_ONLY") == "1")
        {
            await ReplayDailyComputeAsync(input, inputPath);
            return;
        }
        var contract = input.GetProperty("contract_id").GetString()!;
        var api = new MarketDataAnalyticsCommandApi(factory.Services.GetRequiredService<IActorProducer>());
        var completions = new ConcurrentDictionary<Guid, FuturesItiSignalGeneratedCompleteEvent>();
        var failures = new ConcurrentQueue<string>();
        var current = new Dictionary<(string Contract, TimeFrameType Period, DateOnly Bucket), FuturesItiSignalV2ReadModel>();
        var snapshots = new List<FuturesItiSignalV2ReadModel>();
        var listener = new NatsActorEventListener(new NatsEventListenerOptions(), NullLogger<NatsActorEventListener>.Instance);
        await listener.StartAsync($"iti-backtest-{Guid.NewGuid():N}",
            new Dictionary<ActorMailboxId, List<string>>
            {
                [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)] =
                    [FuturesItiSignalGeneratedCompleteEvent.Verb, FuturesItiSignalGeneratedFailEvent.Verb]
            }, ReceiveAsync);
        var accepted = 0;
        var submitted = 0;
        try
        {
            foreach (var tick in ReadInputs(input, inputPath))
            {
                var tickContract = tick.Contract;
                // Compare full nanoseconds before conversion to the production DateTime precision.
                if (tick.VxNanoseconds >= tick.EventNanoseconds || tick.Price <= 0 || tick.VxPrice <= 0)
                    throw new InvalidDataException("Replay requires positive prices and a strictly earlier VX observation.");
                var (date, time, price, vx) = (tick.Date, tick.Timestamp, tick.Price, tick.VxPrice);
                var command = new GenerateFuturesItiSignalCommand(tickContract, date, TimeFrameType.Daily, time, price, vx);
                current.TryGetValue((tickContract, TimeFrameType.Daily, date), out var previous);
                var changed = FuturesItiSignalCompute.TryCompute(command, previous, out var expected);
                accepted++;
                if (accepted % 100_000 == 0)
                    await File.WriteAllTextAsync(inputPath + ".progress.json", JsonSerializer.Serialize(new
                    {
                        evaluated_es_ticks = accepted, submitted_daily_commands = submitted, value_date = date,
                        timestamp = time, signals = snapshots.Count,
                        modes = snapshots.GroupBy(x => x.IntrinsicTimeMode).ToDictionary(x => x.Key.ToString(), x => x.Count())
                    }));
                // No-change evaluation cannot mutate state or drive a completion.
                // Evaluate every input, but persist only state-changing commands.
                if (!changed) continue;
                var response = await api.GenerateFuturesItiSignalAsync(tickContract, date, TimeFrameType.Daily, time, price, vx);
                response.Success.Should().BeTrue();
                submitted++;
                var daily = await WaitForCompletionAsync(response.Value);
                AssertSignal(daily.FuturesItiSignal!, expected);
                current[(tickContract, TimeFrameType.Daily, date)] = expected;
                snapshots.Add(daily.FuturesItiSignal! with { SequenceId = 0 });
                // A completion, rather than every input tick, drives the production hierarchy.
                foreach (var period in FuturesItiSignalTimeFrame.DerivedPeriods)
                {
                    var bucket = FuturesItiSignalTimeFrame.GetCalendarBucketStart(date, period);
                    current.TryGetValue((tickContract, period, bucket), out previous);
                    var child = new GenerateFuturesItiSignalCommand(tickContract, date, period,
                        expected.IntrinsicTime, expected.IntrinsicPrice, vx,
                        FuturesItiSignalTimeFrame.GetCalendarBucketStart(date, period));
                    var childChanged = FuturesItiSignalCompute.TryCompute(child, previous, out var childExpected);
                    var childId = FuturesItiSignalTimeFrame.CreateDerivedCommandId(daily, period);
                    if (childChanged)
                    {
                        var completed = await WaitForCompletionAsync(childId);
                        AssertSignal(completed.FuturesItiSignal!, childExpected);
                        current[(tickContract, period, bucket)] = childExpected;
                        snapshots.Add(completed.FuturesItiSignal! with { SequenceId = 0 });
                    }
                    else
                    {
                        await WaitForAuditAsync(childId);
                    }
                }
            }
            failures.Should().BeEmpty();
            accepted.Should().Be(input.GetProperty("es_rows").GetInt32());
            current.Keys.Select(x => x.Period).Distinct().Should().BeEquivalentTo(new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly });
            foreach (var (key, expected) in current)
            {
                var persisted = await dbFixture.MarketDataDb.GetFuturesItiTimeFrameStateAsync(key.Contract, key.Period, key.Bucket);
                persisted.Should().NotBeNull();
                // PostgreSQL timestamptz stores microseconds; retain the full source
                // nanoseconds in the input proof and compare its exact stored precision.
                AssertSignal(persisted!, expected with
                {
                    IntrinsicTime = new DateTime(expected.IntrinsicTime.Ticks / 10 * 10, expected.IntrinsicTime.Kind)
                });
            }
            var serialized = JsonSerializer.Serialize(snapshots);
            await File.WriteAllTextAsync(inputPath + ".results.json", JsonSerializer.Serialize(new
            {
                input_sha256 = input.GetProperty("input_sha256").GetString(),
                input_file_sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(inputPath))),
                accepted_es_ticks = accepted,
                evaluated_es_ticks = accepted,
                unchanged_es_ticks = accepted - submitted,
                submitted_daily_commands = submitted,
                expected_timeframe_rows = current.Count,
                generated_signals = snapshots.GroupBy(x => x.TimePeriod).ToDictionary(x => x.Key.ToString(), x => x.Count()),
                signal_modes = snapshots.GroupBy(x => x.IntrinsicTimeMode).ToDictionary(x => x.Key.ToString(), x => x.Count()),
                overshoot_examples = snapshots.Where(x => x.IntrinsicTimeMode is IntrinsicTimeModeType.TrendOvershootExtended or IntrinsicTimeModeType.TrendOvershootReversal)
                    .GroupBy(x => x.IntrinsicTimeMode).SelectMany(x => x.Take(10)).Select(x => new
                    {
                        mode = x.IntrinsicTimeMode.ToString(), period = x.TimePeriod.ToString(), x.ValueDate,
                        x.IntrinsicTime, x.IntrinsicPrice, x.BandLevel, x.ReversalLevel, x.Threshold
                    }),
                business_signal_sha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(serialized))),
                initialization = "cold start; every eligible tick evaluated; only changes submitted; audit uses wall clock; business timestamps use source ticks",
                signals = snapshots
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { await listener.StopAsync(); }

        ValueTask ReceiveAsync(string verb, NatsMsg<byte[]> message)
        {
            if (verb == FuturesItiSignalGeneratedCompleteEvent.Verb)
            {
                var completed = message.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!;
                if (completed.EntityId.ContractId == contract || contract == "ES_FRONT") completions[completed.CommandId] = completed;
            }
            else
            {
                var failed = message.AsEvent<FuturesItiSignalGeneratedFailEvent>()!;
                if (failed.EntityId.ContractId == contract || contract == "ES_FRONT") failures.Enqueue(failed.ErrorMessage ?? "ITI projection failed");
            }
            return ValueTask.CompletedTask;
        }

        async Task<FuturesItiSignalGeneratedCompleteEvent> WaitForCompletionAsync(Guid id)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!completions.TryGetValue(id, out _))
            {
                failures.Should().BeEmpty();
                await Task.Delay(10, timeout.Token);
            }
            completions.TryRemove(id, out var completed);
            return completed!;
        }

        async Task WaitForAuditAsync(Guid id)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (await dbFixture.ActorEventSourceDb.GetCommandLogAsync(id) is null)
            {
                failures.Should().BeEmpty();
                await Task.Delay(10, timeout.Token);
            }
        }
    }

    // Strategy research needs Daily states only. This opt-in path exercises the
    // production transition evaluator and transactional storage inside the same
    // isolated integration fixture, without claiming NATS/actor coverage.
    async Task ReplayDailyComputeAsync(JsonElement input, string inputPath)
    {
        var current = new Dictionary<(string Contract, DateOnly Date), FuturesItiSignalV2ReadModel>();
        var snapshots = new List<FuturesItiSignalV2ReadModel>();
        var accepted = 0;
        foreach (var tick in ReadInputs(input, inputPath))
        {
            if (tick.VxNanoseconds >= tick.EventNanoseconds || tick.Price <= 0 || tick.VxPrice <= 0)
                throw new InvalidDataException("Replay requires positive prices and a strictly earlier VX observation.");
            var key = (tick.Contract, tick.Date);
            current.TryGetValue(key, out var previous);
            var command = new GenerateFuturesItiSignalCommand(tick.Contract, tick.Date,
                TimeFrameType.Daily, tick.Timestamp, tick.Price, tick.VxPrice);
            var changed = FuturesItiSignalCompute.TryCompute(command, previous, out var signal);
            accepted++;
            if (changed)
            {
                await dbFixture.MarketDataDb.InsertFuturesItiSignalAsync(signal with { SequenceId = snapshots.Count + 1 });
                current[key] = signal;
                snapshots.Add(signal with { SequenceId = 0 });
            }
            if (accepted % 100_000 == 0)
                await File.WriteAllTextAsync(inputPath + ".progress.json", JsonSerializer.Serialize(new
                {
                    evaluated_es_ticks = accepted, value_date = tick.Date, timestamp = tick.Timestamp,
                    signals = snapshots.Count, replay_mode = "production-daily-compute-and-storage"
                }));
        }
        accepted.Should().Be(input.GetProperty("es_rows").GetInt32());
        foreach (var (key, expected) in current)
        {
            var stored = await dbFixture.MarketDataDb.GetFuturesItiTimeFrameStateAsync(key.Contract, TimeFrameType.Daily, key.Date);
            stored.Should().NotBeNull();
            AssertSignal(stored!, expected with
            {
                IntrinsicTime = new DateTime(expected.IntrinsicTime.Ticks / 10 * 10, expected.IntrinsicTime.Kind)
            });
        }
        var serialized = JsonSerializer.Serialize(snapshots);
        await File.WriteAllTextAsync(inputPath + ".results.json", JsonSerializer.Serialize(new
        {
            input_sha256 = input.GetProperty("input_sha256").GetString(),
            accepted_es_ticks = accepted, evaluated_es_ticks = accepted,
            unchanged_es_ticks = accepted - snapshots.Count,
            expected_timeframe_rows = current.Count,
            generated_signals = new Dictionary<string, int> { ["Daily"] = snapshots.Count },
            business_signal_sha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(serialized))),
            replay_mode = "production-daily-compute-and-transactional-storage; no NATS or command/event audit replay",
            initialization = "cold start per contract and Daily bucket; every eligible tick evaluated; no Weekly/Monthly research required",
            signals = snapshots
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    static void AssertSignal(FuturesItiSignalV2ReadModel actual, FuturesItiSignalV2ReadModel expected)
        => actual.Should().BeEquivalentTo(expected, options => options.Excluding(x => x.SequenceId));

    sealed record ReplayTick(DateOnly Date, DateTime Timestamp, long EventNanoseconds, double Price, long VxNanoseconds, double VxPrice, string Contract);

    static IEnumerable<ReplayTick> ReadInputs(JsonElement input, string inputPath)
    {
        if (!input.TryGetProperty("files", out var files))
        {
            var date = DateOnly.Parse(input.GetProperty("value_date").GetString()!, CultureInfo.InvariantCulture);
            foreach (var tick in input.GetProperty("inputs").EnumerateArray())
                yield return new(date, tick.GetProperty("timestamp").GetDateTime(),
                    long.Parse(tick.GetProperty("event_ns").GetString()!, CultureInfo.InvariantCulture), tick.GetProperty("price").GetDouble(),
                    long.Parse(tick.GetProperty("vx_event_ns").GetString()!, CultureInfo.InvariantCulture), tick.GetProperty("vx_price").GetDouble(), input.GetProperty("contract_id").GetString()!);
            yield break;
        }
        foreach (var file in files.EnumerateArray())
        {
            var root = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
            var name = file.GetProperty("file").GetString()!;
            if (Path.GetFileName(name) != name) throw new InvalidDataException("Replay CSV must be adjacent to its manifest.");
            var path = Path.Combine(root, name);
            using (var stream = File.OpenRead(path))
                Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant().Should().Be(file.GetProperty("sha256").GetString());
            var count = 0;
            long previous = 0;
            foreach (var line in File.ReadLines(path))
            {
                var columns = line.Split(',');
                if (columns.Length != 9) throw new InvalidDataException("Unexpected replay CSV columns.");
                var ns = long.Parse(columns[2], CultureInfo.InvariantCulture);
                if (ns < previous) throw new InvalidDataException("Replay events are not chronological.");
                previous = ns;
                count++;
                yield return new(DateOnly.Parse(columns[0], CultureInfo.InvariantCulture),
                    DateTime.Parse(columns[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), ns,
                    double.Parse(columns[4], CultureInfo.InvariantCulture), long.Parse(columns[5], CultureInfo.InvariantCulture),
                    double.Parse(columns[7], CultureInfo.InvariantCulture), file.TryGetProperty("contract_id", out var fileContract) ? fileContract.GetString()! : input.GetProperty("contract_id").GetString()!);
            }
            count.Should().Be(file.GetProperty("rows").GetInt32());
        }
    }
}
