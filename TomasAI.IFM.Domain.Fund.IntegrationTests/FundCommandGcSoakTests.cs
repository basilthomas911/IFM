using System.Buffers;
using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.Fund.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FundGcSoakCollection
{
    public const string Name = "Fund command GC soak";
}

/// <summary>
/// Manual end-to-end GC soak using Actor.IntegrationTests as the application host.
/// The workload deliberately keeps actor cardinality fixed so post-GC heap growth
/// identifies retained memory rather than thousands of intentionally live actors.
/// </summary>
[Collection(FundGcSoakCollection.Name)]
[Trait("Category", "Manual")]
[Trait("Category", "LongRunning")]
public sealed class FundCommandGcSoakTests(
    WebApplicationFactory<Program> factory,
    FundDatabaseFixture dbFixture,
    ITestOutputHelper output)
    : IClassFixture<WebApplicationFactory<Program>>,
      IClassFixture<FundDatabaseFixture>
{
    static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    readonly ILogger<NatsActorEventListener> _logger =
        Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task FundCommandPath_ReportsGcPressureForConfiguredDuration()
    {
        if (!FundGcSoakOptions.IsEnabled())
            return;

        var options = FundGcSoakOptions.Load();
        var fund = CreateFund(options.FundId, options.PayloadCharacters);
        var subject = new ActorSubject(
            ActorType.Command,
            CreateFundCommand.Actor,
            CreateFundCommand.Verb,
            fund.Id.Format());
        var serializedCommandBytes = GetSerializedCommandSize(fund, subject);
        WebApplicationFactory<Program>? comparisonFactory = null;

        await DeleteFundDataAsync(fund.FundId, subject);
        try
        {
            if (!options.UseOwnedCommandPayloads)
                comparisonFactory = CreateLegacyCommandPayloadFactory(factory);
            var httpClientFactory = new HttpClientTestFactory(comparisonFactory ?? factory);
            httpClientFactory.CreateClient();
            var jsonSerializer = new NewtonSoftJsonSerializer();
            var commandApi = new FundCommandApi(new CommandServiceApiClient(
                httpClientFactory,
                jsonSerializer,
                new CommandServiceApiOptions("http://localhost")));
            var queryApi = new FundQueryApi(new QueryServiceApiClient(
                httpClientFactory,
                jsonSerializer,
                new QueryServiceApiOptions("http://localhost")));

            await CreateAndProjectFundAsync(commandApi, fund);
            await VerifyFundQueryAsync(queryApi, fund.FundId);

            output.WriteLine(
                "Warming Actor.IntegrationTests Fund path: commands={0:N0}, payloadCharacters={1:N0}.",
                options.WarmupCommands,
                options.PayloadCharacters);
            for (var i = 0; i < options.WarmupCommands; i++)
                await SendExpectedDuplicateAsync(commandApi, fund);

            ForceFullCollection();
            var start = GcProcessSnapshot.Capture();
            var started = Stopwatch.GetTimestamp();
            var lastProgress = TimeSpan.Zero;
            long commands = 0;
            long queries = 0;
            long exceptions = 0;
            var exceptionMessages = new List<string>();

            output.WriteLine(
                "Starting Fund GC soak: duration={0}, maxCommands={1}, queryEvery={2:N0}, serverGc={3}.",
                options.Duration,
                options.MaxCommands == 0 ? "unlimited" : options.MaxCommands.ToString("N0"),
                options.QueryEveryCommands,
                GCSettings.IsServerGC);

            while (Stopwatch.GetElapsedTime(started) < options.Duration
                   && (options.MaxCommands == 0 || commands < options.MaxCommands))
            {
                try
                {
                    await SendExpectedDuplicateAsync(commandApi, fund);
                    commands++;

                    if (options.QueryEveryCommands > 0
                        && commands % options.QueryEveryCommands == 0)
                    {
                        await VerifyFundQueryAsync(queryApi, fund.FundId);
                        queries++;
                    }
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
                    WriteProgress(elapsed, commands, queries, start);
                    lastProgress = elapsed;
                }
            }

            var elapsedTotal = Stopwatch.GetElapsedTime(started);
            var endBeforeCollection = GcProcessSnapshot.Capture();
            ForceFullCollection();
            var endAfterCollection = GcProcessSnapshot.Capture();
            var report = FundGcSoakReport.Create(
                options,
                elapsedTotal,
                commands,
                queries,
                exceptions,
                exceptionMessages,
                serializedCommandBytes,
                start,
                endBeforeCollection,
                endAfterCollection);

            var reportJson = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions { WriteIndented = true });
            output.WriteLine("FINAL FUND GC SOAK:{0}{1}", Environment.NewLine, reportJson);
            await WriteReportIfRequestedAsync(options.ReportPath, reportJson);

            Assert.True(commands > 0, "The GC soak did not complete any commands.");
            Assert.True(
                exceptions == 0,
                $"The GC soak recorded {exceptions} exception(s):{Environment.NewLine}"
                + string.Join(Environment.NewLine, exceptionMessages));
            Assert.True(
                report.RetainedHeapGrowthBytes <= options.MaxRetainedHeapBytes,
                $"Post-GC managed heap grew by {report.RetainedHeapGrowthBytes:N0} bytes; "
                + $"configured limit is {options.MaxRetainedHeapBytes:N0} bytes.");
        }
        finally
        {
            comparisonFactory?.Dispose();
            await DeleteFundDataAsync(fund.FundId, subject);
        }
    }

    async Task CreateAndProjectFundAsync(FundCommandApi commandApi, FundReadModel fund)
    {
        var terminalEvent = new TaskCompletionSource<IEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var eventListener = new NatsActorEventListener(
            new NatsEventListenerOptions(),
            _logger);
        try
        {
            await eventListener.StartAsync(
                $"FundGcSoak-{Guid.NewGuid():N}",
                new()
                {
                    [new ActorMailboxId(ActorType.Event, FundCreatedEvent.Actor)] =
                        [FundCreatedCompleteEvent.Verb, FundCreatedFailEvent.Verb]
                },
                (eventVerb, message) =>
                {
                    IEvent @event = eventVerb == FundCreatedCompleteEvent.Verb
                        ? message.AsEvent<FundCreatedCompleteEvent>()!
                        : message.AsEvent<FundCreatedFailEvent>()!;
                    terminalEvent.TrySetResult(@event);
                    return ValueTask.CompletedTask;
                });

            var response = await commandApi.CreateFundAsync(fund).WaitAsync(RequestTimeout);
            Assert.True(response.Success, response.ErrorMessage);
            Assert.NotEqual(Guid.Empty, response.Value);

            var projected = await terminalEvent.Task.WaitAsync(EventTimeout);
            Assert.IsType<FundCreatedCompleteEvent>(projected);
        }
        finally
        {
            await eventListener.StopAsync();
        }
    }

    static async Task SendExpectedDuplicateAsync(
        FundCommandApi commandApi,
        FundReadModel fund)
    {
        var response = await commandApi.CreateFundAsync(fund).WaitAsync(RequestTimeout);
        if (response.Success
            || string.IsNullOrWhiteSpace(response.ErrorMessage)
            || !response.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The bounded-cardinality command returned an unexpected result: "
                + $"success={response.Success}, error={response.ErrorMessage ?? "none"}.");
        }
    }

    static async Task VerifyFundQueryAsync(FundQueryApi queryApi, int fundId)
    {
        var response = await queryApi.GetFundsAsync().WaitAsync(RequestTimeout);
        if (!response.Success
            || response.Value is null
            || !response.Value.Any(fund => fund.FundId == fundId))
        {
            throw new InvalidOperationException(
                $"Fund query did not return GC soak fund {fundId}: {response.ErrorMessage}");
        }
    }

    void WriteProgress(
        TimeSpan elapsed,
        long commands,
        long queries,
        GcProcessSnapshot start)
    {
        var current = GcProcessSnapshot.Capture();
        var rate = elapsed.TotalSeconds <= 0 ? 0 : commands / elapsed.TotalSeconds;
        output.WriteLine(
            "[{0:hh\\:mm\\:ss}] commands={1:N0}, rate={2:N1}/s, queries={3:N0}, "
            + "allocated={4:N0} MB, gen0={5:N0}, gen1={6:N0}, gen2={7:N0}, "
            + "gcPause={8:N1} ms, heap={9:N1} MB, workingSet={10:N1} MB.",
            elapsed,
            commands,
            rate,
            queries,
            BytesToMegabytes(current.TotalAllocatedBytes - start.TotalAllocatedBytes),
            current.Gen0Collections - start.Gen0Collections,
            current.Gen1Collections - start.Gen1Collections,
            current.Gen2Collections - start.Gen2Collections,
            (current.TotalPauseDuration - start.TotalPauseDuration).TotalMilliseconds,
            BytesToMegabytes(current.HeapSizeBytes),
            BytesToMegabytes(current.WorkingSetBytes));
    }

    async Task DeleteFundDataAsync(int fundId, ActorSubject subject)
    {
        var streamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync(
            subject.ThreadId.ToString());
        if (streamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(streamId);
        await dbFixture.FundDb.DeleteFundAsync(fundId);
    }

    static FundReadModel CreateFund(int fundId, int payloadCharacters) => new(
        fundId,
        $"GC Soak Fund {fundId}",
        CreateIncompressibleText(payloadCharacters),
        100_000m,
        false,
        DateTime.UtcNow,
        "FundCommandGcSoakTests");

    static string CreateIncompressibleText(int length)
    {
        const string alphabet =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random(0x5EED);
        return string.Create(length, random, static (destination, state) =>
        {
            for (var i = 0; i < destination.Length; i++)
                destination[i] = alphabet[state.Next(alphabet.Length)];
        });
    }

    static int GetSerializedCommandSize(FundReadModel fund, ActorSubject subject)
    {
        var command = new CreateFundCommand(fund)
        {
            CommandId = Guid.NewGuid(),
            Subject = subject,
            PostEvents = true
        };
        var writer = new ArrayBufferWriter<byte>();
        NatsMessagePackSerializer<CreateFundCommand>.Default.Serialize(writer, command);
        return writer.WrittenCount;
    }

    static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    static async Task WriteReportIfRequestedAsync(string? reportPath, string json)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
            return;
        var fullPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(fullPath, json);
    }

    static WebApplicationFactory<Program> CreateLegacyCommandPayloadFactory(
        WebApplicationFactory<Program> sourceFactory) =>
        sourceFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<INatsConsumerOptions>();
            services.AddSingleton<INatsConsumerOptions>(new NatsConsumerOptions
            {
                UseOwnedCommandPayloads = false
            });
        }));

    static double BytesToMegabytes(long bytes) => bytes / 1024d / 1024d;
}

internal sealed record FundGcSoakOptions(
    TimeSpan Duration,
    TimeSpan ProgressInterval,
    int WarmupCommands,
    long MaxCommands,
    int QueryEveryCommands,
    int PayloadCharacters,
    int FundId,
    long MaxRetainedHeapBytes,
    bool UseOwnedCommandPayloads,
    string? ReportPath)
{
    internal static bool IsEnabled() => string.Equals(
        Environment.GetEnvironmentVariable("IFM_RUN_FUND_GC_SOAK"),
        "1",
        StringComparison.Ordinal);

    internal static FundGcSoakOptions Load() => new(
        Duration: TimeSpan.FromSeconds(GetPositiveInt("IFM_FUND_GC_SOAK_SECONDS", 600)),
        ProgressInterval: TimeSpan.FromSeconds(GetPositiveInt("IFM_FUND_GC_PROGRESS_SECONDS", 30)),
        WarmupCommands: GetNonNegativeInt("IFM_FUND_GC_WARMUP_COMMANDS", 25),
        MaxCommands: GetNonNegativeLong("IFM_FUND_GC_MAX_COMMANDS", 0),
        QueryEveryCommands: GetNonNegativeInt("IFM_FUND_GC_QUERY_EVERY", 100),
        PayloadCharacters: GetPositiveInt("IFM_FUND_GC_PAYLOAD_CHARACTERS", 4096),
        FundId: GetPositiveInt(
            "IFM_FUND_GC_FUND_ID",
            Random.Shared.Next(1_500_000_000, 2_000_000_000)),
        MaxRetainedHeapBytes: checked(
            GetPositiveLong("IFM_FUND_GC_MAX_RETAINED_MB", 128) * 1024 * 1024),
        UseOwnedCommandPayloads: !IsOne("IFM_FUND_GC_USE_LEGACY_COMMAND_PAYLOADS"),
        ReportPath: Environment.GetEnvironmentVariable("IFM_FUND_GC_REPORT_PATH"));

    static bool IsOne(string name) => string.Equals(
        Environment.GetEnvironmentVariable(name),
        "1",
        StringComparison.Ordinal);

    static int GetPositiveInt(string name, int defaultValue)
    {
        var value = GetInt(name, defaultValue);
        return value > 0
            ? value
            : throw new InvalidOperationException($"{name} must be greater than zero.");
    }

    static int GetNonNegativeInt(string name, int defaultValue)
    {
        var value = GetInt(name, defaultValue);
        return value >= 0
            ? value
            : throw new InvalidOperationException($"{name} cannot be negative.");
    }

    static long GetPositiveLong(string name, long defaultValue)
    {
        var value = GetLong(name, defaultValue);
        return value > 0
            ? value
            : throw new InvalidOperationException($"{name} must be greater than zero.");
    }

    static long GetNonNegativeLong(string name, long defaultValue)
    {
        var value = GetLong(name, defaultValue);
        return value >= 0
            ? value
            : throw new InvalidOperationException($"{name} cannot be negative.");
    }

    static int GetInt(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        return int.TryParse(raw, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"{name} must be a whole number.");
    }

    static long GetLong(string name, long defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        return long.TryParse(raw, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"{name} must be a whole number.");
    }
}

internal sealed record GcProcessSnapshot(
    DateTimeOffset Timestamp,
    long TotalAllocatedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    TimeSpan TotalPauseDuration,
    long HeapSizeBytes,
    long FragmentedBytes,
    long TotalCommittedBytes,
    long WorkingSetBytes,
    long PrivateMemoryBytes)
{
    internal static GcProcessSnapshot Capture()
    {
        var totalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true);
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);
        var totalPauseDuration = GC.GetTotalPauseDuration();
        var memory = GC.GetGCMemoryInfo();
        using var process = Process.GetCurrentProcess();
        return new(
            DateTimeOffset.UtcNow,
            totalAllocatedBytes,
            gen0Collections,
            gen1Collections,
            gen2Collections,
            totalPauseDuration,
            memory.HeapSizeBytes,
            memory.FragmentedBytes,
            memory.TotalCommittedBytes,
            process.WorkingSet64,
            process.PrivateMemorySize64);
    }
}

internal sealed record FundGcSoakReport(
    DateTimeOffset CreatedAt,
    string Framework,
    bool ServerGc,
    FundGcSoakOptions Options,
    TimeSpan Elapsed,
    long Commands,
    long Queries,
    long Exceptions,
    IReadOnlyList<string> ExceptionMessages,
    int SerializedCommandBytes,
    double CommandsPerSecond,
    long AllocatedBytes,
    double AllocatedBytesPerCommand,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    double GcPauseMilliseconds,
    double GcPausePercent,
    long EndHeapSizeBytes,
    long RetainedHeapGrowthBytes,
    long WorkingSetGrowthBytes,
    GcProcessSnapshot Start,
    GcProcessSnapshot EndBeforeCollection,
    GcProcessSnapshot EndAfterCollection)
{
    internal static FundGcSoakReport Create(
        FundGcSoakOptions options,
        TimeSpan elapsed,
        long commands,
        long queries,
        long exceptions,
        IReadOnlyList<string> exceptionMessages,
        int serializedCommandBytes,
        GcProcessSnapshot start,
        GcProcessSnapshot endBeforeCollection,
        GcProcessSnapshot endAfterCollection)
    {
        var allocated = endBeforeCollection.TotalAllocatedBytes - start.TotalAllocatedBytes;
        var pause = endBeforeCollection.TotalPauseDuration - start.TotalPauseDuration;
        return new(
            DateTimeOffset.UtcNow,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            GCSettings.IsServerGC,
            options,
            elapsed,
            commands,
            queries,
            exceptions,
            exceptionMessages,
            serializedCommandBytes,
            elapsed.TotalSeconds <= 0 ? 0 : commands / elapsed.TotalSeconds,
            allocated,
            commands == 0 ? 0 : (double)allocated / commands,
            endBeforeCollection.Gen0Collections - start.Gen0Collections,
            endBeforeCollection.Gen1Collections - start.Gen1Collections,
            endBeforeCollection.Gen2Collections - start.Gen2Collections,
            pause.TotalMilliseconds,
            elapsed.TotalMilliseconds <= 0
                ? 0
                : pause.TotalMilliseconds / elapsed.TotalMilliseconds * 100,
            endBeforeCollection.HeapSizeBytes,
            endAfterCollection.HeapSizeBytes - start.HeapSizeBytes,
            endAfterCollection.WorkingSetBytes - start.WorkingSetBytes,
            start,
            endBeforeCollection,
            endAfterCollection);
    }
}
