using System.Buffers;
using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.Fund.IntegrationTests;

/// <summary>
/// Manual production-like event ingress/ACK/GC soak hosted by
/// Actor.IntegrationTests. FundCreated is intentionally not handled by
/// FundEventActor, making this a bounded-cardinality transport and mailbox
/// ownership workload without database side effects.
/// </summary>
[Collection(FundGcSoakCollection.Name)]
[Trait("Category", "Manual")]
[Trait("Category", "LongRunning")]
public sealed class FundEventGcSoakTests(
    WebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : IClassFixture<WebApplicationFactory<Program>>
{
    const int SoakFundId = 1_900_000_001;
    static readonly string SoakSubject = new ActorSubject(
        ActorType.Event,
        FundCreatedEvent.Actor,
        FundCreatedEvent.Verb,
        SoakFundId.ToString()).ToString();

    [Fact]
    public async Task FundEventPath_ReportsGcPressureForConfiguredDuration()
    {
        if (!FundEventGcSoakOptions.IsEnabled())
            return;

        var options = FundEventGcSoakOptions.Load();
        WebApplicationFactory<Program>? comparisonFactory = null;
        try
        {
            comparisonFactory = CreateEventPayloadFactory(
                factory,
                options.UseOwnedEventPayloads);
            var host = comparisonFactory ?? factory;
            using var client = host.CreateClient();
            var supervisor = host.Services.GetRequiredService<IActorSupervisor>();
            var producer = supervisor.GetJSProducer(
                new ActorMailboxId(ActorType.Event, FundCreatedEvent.Actor));
            var @event = CreateEvent(options.PayloadCharacters);
            var serializedBytes = GetSerializedSize(@event);

            for (var index = 0; index < options.WarmupEvents; index++)
                await producer.SendAsync<FundCreatedEvent, FundId>(@event.Subject, @event);
            await WaitForEventConsumerDrainAsync(host.Services, options.DrainTimeout);

            ForceFullCollection();
            var start = GcProcessSnapshot.Capture();
            var started = Stopwatch.GetTimestamp();
            var lastProgress = TimeSpan.Zero;
            long events = 0;
            long exceptions = 0;
            var exceptionMessages = new List<string>();

            while (Stopwatch.GetElapsedTime(started) < options.Duration
                   && (options.MaxEvents == 0 || events < options.MaxEvents))
            {
                try
                {
                    await producer.SendAsync<FundCreatedEvent, FundId>(@event.Subject, @event);
                    events++;
                }
                catch (Exception exception)
                {
                    exceptions++;
                    exceptionMessages.Add(
                        $"{DateTimeOffset.UtcNow:O} {exception.GetType().Name}: {exception.Message}");
                    break;
                }

                var elapsed = Stopwatch.GetElapsedTime(started);
                if (elapsed - lastProgress >= options.ProgressInterval)
                {
                    WriteProgress(elapsed, events, start);
                    lastProgress = elapsed;
                }
            }

            await WaitForEventConsumerDrainAsync(host.Services, options.DrainTimeout);
            var elapsedTotal = Stopwatch.GetElapsedTime(started);
            var endBeforeCollection = GcProcessSnapshot.Capture();
            ForceFullCollection();
            var endAfterCollection = GcProcessSnapshot.Capture();
            var report = FundEventGcSoakReport.Create(
                options,
                elapsedTotal,
                events,
                exceptions,
                exceptionMessages,
                serializedBytes,
                start,
                endBeforeCollection,
                endAfterCollection);
            var json = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions { WriteIndented = true });
            output.WriteLine("FINAL FUND EVENT GC SOAK:{0}{1}", Environment.NewLine, json);
            await WriteReportIfRequestedAsync(options.ReportPath, json);

            Assert.True(events > 0, "The event GC soak did not publish any events.");
            Assert.True(
                exceptions == 0,
                $"The event GC soak recorded {exceptions} exception(s):{Environment.NewLine}"
                + string.Join(Environment.NewLine, exceptionMessages));
            Assert.True(
                report.RetainedHeapGrowthBytes <= options.MaxRetainedHeapBytes,
                $"Post-GC managed heap grew by {report.RetainedHeapGrowthBytes:N0} bytes; "
                + $"configured limit is {options.MaxRetainedHeapBytes:N0} bytes.");
        }
        finally
        {
            comparisonFactory?.Dispose();
        }
    }

    void WriteProgress(TimeSpan elapsed, long events, GcProcessSnapshot start)
    {
        var current = GcProcessSnapshot.Capture();
        output.WriteLine(
            "[{0:hh\\:mm\\:ss}] events={1:N0}, rate={2:N1}/s, allocated={3:N1} MB, "
            + "gen0={4:N0}, gen1={5:N0}, gen2={6:N0}, heap={7:N1} MB.",
            elapsed,
            events,
            elapsed.TotalSeconds <= 0 ? 0 : events / elapsed.TotalSeconds,
            ToMegabytes(current.TotalAllocatedBytes - start.TotalAllocatedBytes),
            current.Gen0Collections - start.Gen0Collections,
            current.Gen1Collections - start.Gen1Collections,
            current.Gen2Collections - start.Gen2Collections,
            ToMegabytes(current.HeapSizeBytes));
    }

    static FundCreatedEvent CreateEvent(int payloadCharacters)
    {
        var fundId = SoakFundId;
        var fund = new FundReadModel(
            fundId,
            "Event GC Soak Fund",
            new string('x', payloadCharacters),
            100_000m,
            false,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            nameof(FundEventGcSoakTests));
        return new FundCreatedEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FundCreatedEvent.Actor,
                FundCreatedEvent.Verb,
                fundId.ToString()),
            Id = Guid.Parse("428d87ec-b786-4d70-961e-3e6663781705"),
            EntityId = new FundId(fundId),
            EventId = 1,
            CommandId = Guid.Parse("42a68c60-fc8f-42ba-9080-c31e9aa70ba8"),
            AggregateId = fundId.ToString(),
            EventSource = nameof(FundEventGcSoakTests),
            ReceivedOn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NewFund = fund
        };
    }

    static int GetSerializedSize(FundCreatedEvent @event)
    {
        var writer = new ArrayBufferWriter<byte>();
        NatsMessagePackSerializer<FundCreatedEvent>.Default.Serialize(writer, @event);
        return writer.WrittenCount;
    }

    static async Task WaitForEventConsumerDrainAsync(
        IServiceProvider services,
        TimeSpan timeout)
    {
        var manager = services.GetRequiredService<NatsConnectionManager>();
        var options = services.GetRequiredService<INatsJetStreamConsumerOptions>();
        var context = await manager.GetJetStreamContextAsync(options.Url);
        var streamName = string.IsNullOrWhiteSpace(options.StreamName)
            ? "EventStream"
            : options.StreamName;
        var consumerName = string.IsNullOrWhiteSpace(options.DurableConsumerName)
            ? "EventConsumer"
            : options.DurableConsumerName;
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < timeout)
        {
            var consumer = await context.GetConsumerAsync(streamName, consumerName);
            if (consumer.Info.NumAckPending == 0 && consumer.Info.NumPending == 0)
                return;
            await Task.Delay(25);
        }
        throw new TimeoutException(
            $"JetStream consumer {streamName}/{consumerName} did not drain within {timeout}.");
    }

    static WebApplicationFactory<Program> CreateEventPayloadFactory(
        WebApplicationFactory<Program> sourceFactory,
        bool useOwnedEventPayloads) =>
        sourceFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<INatsJetStreamConsumerOptions>();
            services.AddSingleton<INatsJetStreamConsumerOptions>(new NatsJetStreamConsumerOptions
            {
                DurableConsumerName = $"EventGcSoak-{Guid.NewGuid():N}",
                FilterSubject = SoakSubject,
                UseOwnedEventPayloads = useOwnedEventPayloads
            });
        }));

    static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    static async Task WriteReportIfRequestedAsync(string? path, string json)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(fullPath, json);
    }

    static double ToMegabytes(long bytes) => bytes / 1024d / 1024d;
}

internal sealed record FundEventGcSoakOptions(
    TimeSpan Duration,
    TimeSpan ProgressInterval,
    TimeSpan DrainTimeout,
    int WarmupEvents,
    long MaxEvents,
    int PayloadCharacters,
    long MaxRetainedHeapBytes,
    bool UseOwnedEventPayloads,
    string? ReportPath)
{
    internal static bool IsEnabled() => IsOne("IFM_RUN_FUND_EVENT_GC_SOAK");

    internal static FundEventGcSoakOptions Load() => new(
        TimeSpan.FromSeconds(GetPositiveInt("IFM_FUND_EVENT_GC_SOAK_SECONDS", 600)),
        TimeSpan.FromSeconds(GetPositiveInt("IFM_FUND_EVENT_GC_PROGRESS_SECONDS", 30)),
        TimeSpan.FromSeconds(GetPositiveInt("IFM_FUND_EVENT_GC_DRAIN_SECONDS", 30)),
        GetNonNegativeInt("IFM_FUND_EVENT_GC_WARMUP_EVENTS", 100),
        GetNonNegativeLong("IFM_FUND_EVENT_GC_MAX_EVENTS", 0),
        GetPositiveInt("IFM_FUND_EVENT_GC_PAYLOAD_CHARACTERS", 4096),
        checked(GetPositiveLong("IFM_FUND_EVENT_GC_MAX_RETAINED_MB", 128) * 1024 * 1024),
        !IsOne("IFM_FUND_EVENT_GC_USE_LEGACY_PAYLOADS"),
        Environment.GetEnvironmentVariable("IFM_FUND_EVENT_GC_REPORT_PATH"));

    static bool IsOne(string name) => string.Equals(
        Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);

    static int GetPositiveInt(string name, int fallback)
    {
        var value = GetInt(name, fallback);
        return value > 0 ? value : throw new InvalidOperationException($"{name} must be positive.");
    }

    static int GetNonNegativeInt(string name, int fallback)
    {
        var value = GetInt(name, fallback);
        return value >= 0 ? value : throw new InvalidOperationException($"{name} cannot be negative.");
    }

    static long GetPositiveLong(string name, long fallback)
    {
        var value = GetLong(name, fallback);
        return value > 0 ? value : throw new InvalidOperationException($"{name} must be positive.");
    }

    static long GetNonNegativeLong(string name, long fallback)
    {
        var value = GetLong(name, fallback);
        return value >= 0 ? value : throw new InvalidOperationException($"{name} cannot be negative.");
    }

    static int GetInt(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : int.TryParse(value, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"{name} must be a whole number.");
    }

    static long GetLong(string name, long fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : long.TryParse(value, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"{name} must be a whole number.");
    }
}

internal sealed record FundEventGcSoakReport(
    DateTimeOffset CreatedAt,
    string Framework,
    bool ServerGc,
    FundEventGcSoakOptions Options,
    TimeSpan Elapsed,
    long Events,
    long Exceptions,
    IReadOnlyList<string> ExceptionMessages,
    int SerializedEventBytes,
    double EventsPerSecond,
    long AllocatedBytes,
    double AllocatedBytesPerEvent,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    double GcPauseMilliseconds,
    long RetainedHeapGrowthBytes,
    GcProcessSnapshot Start,
    GcProcessSnapshot EndBeforeCollection,
    GcProcessSnapshot EndAfterCollection)
{
    internal static FundEventGcSoakReport Create(
        FundEventGcSoakOptions options,
        TimeSpan elapsed,
        long events,
        long exceptions,
        IReadOnlyList<string> exceptionMessages,
        int serializedEventBytes,
        GcProcessSnapshot start,
        GcProcessSnapshot endBeforeCollection,
        GcProcessSnapshot endAfterCollection)
    {
        var allocated = endBeforeCollection.TotalAllocatedBytes - start.TotalAllocatedBytes;
        return new(
            DateTimeOffset.UtcNow,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            GCSettings.IsServerGC,
            options,
            elapsed,
            events,
            exceptions,
            exceptionMessages,
            serializedEventBytes,
            elapsed.TotalSeconds <= 0 ? 0 : events / elapsed.TotalSeconds,
            allocated,
            events == 0 ? 0 : (double)allocated / events,
            endBeforeCollection.Gen0Collections - start.Gen0Collections,
            endBeforeCollection.Gen1Collections - start.Gen1Collections,
            endBeforeCollection.Gen2Collections - start.Gen2Collections,
            (endBeforeCollection.TotalPauseDuration - start.TotalPauseDuration).TotalMilliseconds,
            endAfterCollection.HeapSizeBytes - start.HeapSizeBytes,
            start,
            endBeforeCollection,
            endAfterCollection);
    }
}
