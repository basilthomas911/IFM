using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.Fund.IntegrationTests;

/// <summary>
/// Manually enabled end-to-end query GC soak hosted by Actor.IntegrationTests.
/// A fixed Fund query keeps actor and database cardinality stable so post-GC
/// growth exposes retained reply contexts or message buffers.
/// </summary>
[Collection(FundGcSoakCollection.Name)]
[Trait("Category", "Manual")]
[Trait("Category", "LongRunning")]
public sealed class FundQueryGcSoakTests(
    WebApplicationFactory<Program> factory,
    FundDatabaseFixture dbFixture,
    ITestOutputHelper output)
    : IClassFixture<WebApplicationFactory<Program>>,
      IClassFixture<FundDatabaseFixture>
{
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task FundQueryPath_ReportsGcPressureForConfiguredDuration()
    {
        if (!FundQueryGcSoakOptions.IsEnabled())
            return;

        var options = FundQueryGcSoakOptions.Load();
        var fund = SampleData.NewFund;
        var transaction = SampleData.FundTransaction;
        WebApplicationFactory<Program>? comparisonFactory = null;

        await DeleteDataAsync(fund.FundId);
        try
        {
            await dbFixture.FundDb.InsertFundAsync(fund);
            await dbFixture.FundDb.InsertFundTransactionAsync(transaction);
            if (!options.UseOwnedQueryPayloads)
                comparisonFactory = CreateLegacyQueryPayloadFactory(factory);

            var httpClientFactory = new HttpClientTestFactory(comparisonFactory ?? factory);
            httpClientFactory.CreateClient();
            var queryApi = new FundQueryApi(new QueryServiceApiClient(
                httpClientFactory,
                new NewtonSoftJsonSerializer(),
                new QueryServiceApiOptions("http://localhost")));

            output.WriteLine(
                "Warming Actor.IntegrationTests Fund query path: queries={0:N0}, ownedPayloads={1}.",
                options.WarmupQueries,
                options.UseOwnedQueryPayloads);
            for (var i = 0; i < options.WarmupQueries; i++)
                await VerifyFundBalanceAsync(queryApi, fund.FundId, transaction.Balance);

            ForceFullCollection();
            var start = GcProcessSnapshot.Capture();
            var started = Stopwatch.GetTimestamp();
            var lastProgress = TimeSpan.Zero;
            long queries = 0;
            long exceptions = 0;
            var exceptionMessages = new List<string>();

            while (Stopwatch.GetElapsedTime(started) < options.Duration
                   && (options.MaxQueries == 0 || queries < options.MaxQueries))
            {
                try
                {
                    await VerifyFundBalanceAsync(queryApi, fund.FundId, transaction.Balance);
                    queries++;
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
                    WriteProgress(elapsed, queries, start);
                    lastProgress = elapsed;
                }
            }

            var elapsedTotal = Stopwatch.GetElapsedTime(started);
            var endBeforeCollection = GcProcessSnapshot.Capture();
            ForceFullCollection();
            var endAfterCollection = GcProcessSnapshot.Capture();
            var report = FundQueryGcSoakReport.Create(
                options,
                elapsedTotal,
                queries,
                exceptions,
                exceptionMessages,
                start,
                endBeforeCollection,
                endAfterCollection);
            var reportJson = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions { WriteIndented = true });
            output.WriteLine("FINAL FUND QUERY GC SOAK:{0}{1}", Environment.NewLine, reportJson);
            await WriteReportIfRequestedAsync(options.ReportPath, reportJson);

            Assert.True(queries > 0, "The GC soak did not complete any queries.");
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
            await DeleteDataAsync(fund.FundId);
        }
    }

    static async Task VerifyFundBalanceAsync(
        FundQueryApi queryApi,
        int fundId,
        decimal expectedBalance)
    {
        var response = await queryApi.GetFundBalanceAsync(fundId).WaitAsync(RequestTimeout);
        if (!response.Success || response.Value?.Value != expectedBalance)
        {
            throw new InvalidOperationException(
                $"Fund balance query failed for {fundId}: {response.ErrorMessage}");
        }
    }

    void WriteProgress(TimeSpan elapsed, long queries, GcProcessSnapshot start)
    {
        var current = GcProcessSnapshot.Capture();
        var rate = elapsed.TotalSeconds <= 0 ? 0 : queries / elapsed.TotalSeconds;
        output.WriteLine(
            "[{0:hh\\:mm\\:ss}] queries={1:N0}, rate={2:N1}/s, allocated={3:N1} MB, "
            + "gen0={4:N0}, gen1={5:N0}, gen2={6:N0}, gcPause={7:N1} ms, heap={8:N1} MB.",
            elapsed,
            queries,
            rate,
            BytesToMegabytes(current.TotalAllocatedBytes - start.TotalAllocatedBytes),
            current.Gen0Collections - start.Gen0Collections,
            current.Gen1Collections - start.Gen1Collections,
            current.Gen2Collections - start.Gen2Collections,
            (current.TotalPauseDuration - start.TotalPauseDuration).TotalMilliseconds,
            BytesToMegabytes(current.HeapSizeBytes));
    }

    async Task DeleteDataAsync(int fundId)
    {
        await dbFixture.FundDb.UseTest(
            $"delete from fund_transaction where fundid = {fundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.DeleteFundAsync(fundId);
    }

    static WebApplicationFactory<Program> CreateLegacyQueryPayloadFactory(
        WebApplicationFactory<Program> sourceFactory) =>
        sourceFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<INatsConsumerOptions>();
            services.AddSingleton<INatsConsumerOptions>(new NatsConsumerOptions
            {
                UseOwnedQueryPayloads = false
            });
        }));

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

    static double BytesToMegabytes(long bytes) => bytes / 1024d / 1024d;
}

internal sealed record FundQueryGcSoakOptions(
    TimeSpan Duration,
    TimeSpan ProgressInterval,
    int WarmupQueries,
    long MaxQueries,
    long MaxRetainedHeapBytes,
    bool UseOwnedQueryPayloads,
    string? ReportPath)
{
    internal static bool IsEnabled() => IsOne("IFM_RUN_FUND_QUERY_GC_SOAK");

    internal static FundQueryGcSoakOptions Load() => new(
        Duration: TimeSpan.FromSeconds(GetPositiveInt("IFM_FUND_QUERY_GC_SOAK_SECONDS", 600)),
        ProgressInterval: TimeSpan.FromSeconds(
            GetPositiveInt("IFM_FUND_QUERY_GC_PROGRESS_SECONDS", 30)),
        WarmupQueries: GetNonNegativeInt("IFM_FUND_QUERY_GC_WARMUP_QUERIES", 100),
        MaxQueries: GetNonNegativeLong("IFM_FUND_QUERY_GC_MAX_QUERIES", 0),
        MaxRetainedHeapBytes: checked(
            GetPositiveLong("IFM_FUND_QUERY_GC_MAX_RETAINED_MB", 128) * 1024 * 1024),
        UseOwnedQueryPayloads: !IsOne("IFM_FUND_QUERY_GC_USE_LEGACY_PAYLOADS"),
        ReportPath: Environment.GetEnvironmentVariable("IFM_FUND_QUERY_GC_REPORT_PATH"));

    static bool IsOne(string name) => string.Equals(
        Environment.GetEnvironmentVariable(name),
        "1",
        StringComparison.Ordinal);

    static int GetPositiveInt(string name, int defaultValue)
    {
        var value = GetInt(name, defaultValue);
        return value > 0 ? value : throw new InvalidOperationException($"{name} must be greater than zero.");
    }

    static int GetNonNegativeInt(string name, int defaultValue)
    {
        var value = GetInt(name, defaultValue);
        return value >= 0 ? value : throw new InvalidOperationException($"{name} cannot be negative.");
    }

    static long GetPositiveLong(string name, long defaultValue)
    {
        var value = GetLong(name, defaultValue);
        return value > 0 ? value : throw new InvalidOperationException($"{name} must be greater than zero.");
    }

    static long GetNonNegativeLong(string name, long defaultValue)
    {
        var value = GetLong(name, defaultValue);
        return value >= 0 ? value : throw new InvalidOperationException($"{name} cannot be negative.");
    }

    static int GetInt(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw)
            ? defaultValue
            : int.TryParse(raw, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"{name} must be a whole number.");
    }

    static long GetLong(string name, long defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw)
            ? defaultValue
            : long.TryParse(raw, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"{name} must be a whole number.");
    }
}

internal sealed record FundQueryGcSoakReport(
    DateTimeOffset CreatedAt,
    string Framework,
    bool ServerGc,
    FundQueryGcSoakOptions Options,
    TimeSpan Elapsed,
    long Queries,
    long Exceptions,
    IReadOnlyList<string> ExceptionMessages,
    double QueriesPerSecond,
    long AllocatedBytes,
    double AllocatedBytesPerQuery,
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
    internal static FundQueryGcSoakReport Create(
        FundQueryGcSoakOptions options,
        TimeSpan elapsed,
        long queries,
        long exceptions,
        IReadOnlyList<string> exceptionMessages,
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
            queries,
            exceptions,
            exceptionMessages,
            elapsed.TotalSeconds <= 0 ? 0 : queries / elapsed.TotalSeconds,
            allocated,
            queries == 0 ? 0 : (double)allocated / queries,
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
