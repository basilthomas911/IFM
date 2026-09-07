using Cassandra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

/// <summary>Real command/projector/query actors with an owned temporary Scylla keyspace.</summary>
[Trait("Category", "Integration")]
[Collection("DownloadLog runtime")]
public sealed class MarketConditionDownloadLogIntegrationTests : IAsyncLifetime
{
    readonly string keyspace = "mc_dl_test_" + Guid.NewGuid().ToString("N");
    Cluster cluster = null!;
    ISession session = null!;
    MarketDataFixture fixture = null!;
    WebApplicationFactory<Program> root = null!;
    WebApplicationFactory<Program> host = null!;

    public async Task InitializeAsync()
    {
        cluster = Cluster.Builder().AddContactPoint("localhost").Build();
        session = await cluster.ConnectAsync();
        await session.ExecuteAsync(new SimpleStatement($"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};"));
        fixture = new MarketDataFixture(keyspace);
        root = new WebApplicationFactory<Program>();
        host = root.WithWebHostBuilder(builder => builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.MarketData")
            .UseSetting("IFM_TEST_NATS_URL", Environment.GetEnvironmentVariable("IFM_DOWNLOADLOG_TEST_NATS_URL") ?? "nats://127.0.0.1:14222")
            .UseSetting("IFM_TEST_MARKET_DATA_CONNECTION", $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}"));
        _ = host.Services;
    }

    [Fact]
    public async Task Live_consumer_requires_projected_calendar_success_and_never_masks_newer_failure()
    {
        var commands = host.Services.GetRequiredService<IDownloadLogCommandApi>();
        var queries = host.Services.GetRequiredService<IDownloadLogQueryApi>();
        var adapter = new MarketConditionEventRiskAdapter(fixture.DbFactory, queries);
        var at = DateTime.UtcNow;
        var missing = await adapter.ReadOnceAsync(new(), at, default);
        Assert.False(missing.DownloadEvidence!.CoverageConfirmed);
        Assert.Equal(MarketEventRiskStatus.Unknown, missing.Status);

        // Treasury completion cannot satisfy economic-calendar coverage.
        var treasury = Outcome(DateOnly.FromDateTime(at), "US", MarketDataDownloadDataset.TreasuryCurve);
        await RecordAndWait(treasury);
        Assert.False((await adapter.ReadOnceAsync(new(), DateTime.UtcNow, default)).DownloadEvidence!.CoverageConfirmed);

        // Cover dates on either side of a possible UTC-midnight event window.
        foreach (var date in new[] { DateOnly.FromDateTime(at).AddDays(-1), DateOnly.FromDateTime(at), DateOnly.FromDateTime(at).AddDays(1) })
            await RecordAndWait(Outcome(date));
        var clear = await adapter.ReadOnceAsync(new(), DateTime.UtcNow, default);
        Assert.True(clear.DownloadEvidence!.CoverageConfirmed);
        Assert.Equal(MarketEventRiskStatus.Clear, clear.Status);
        Assert.All(clear.DownloadEvidence.Attempts, x => Assert.Equal(MarketDataDownloadDataset.EconomicCalendar, x.Outcome.Dataset));

        // A real stored event is still classified by the production adapter.
        var eventAt = DateTime.UtcNow;
        await fixture.MarketDataDb.InsertEconomicCalendarsAsync(
            [new EconomicCalendarReadModel(eventAt.AddMinutes(5), "US", "FOMC Rate Decision", null, null, null, eventAt, "test", "High")]);
        Assert.Equal(MarketEventRiskStatus.Blocked, (await adapter.ReadOnceAsync(new(), DateTime.UtcNow, default)).Status);

        var failed = Outcome(DateOnly.FromDateTime(DateTime.UtcNow), "US") with
        {
            Status = MarketDataDownloadStatus.Failed, DownloadedRecordCount = 1, PersistedRecordCount = null,
            ErrorCode = "StorageFailed", ErrorMessage = "Partial write could not be confirmed."
        };
        await RecordAndWait(failed);
        var unavailable = await adapter.ReadOnceAsync(new(), DateTime.UtcNow, default);
        Assert.False(unavailable.DownloadEvidence!.CoverageConfirmed);
        Assert.Equal("CalendarDownloadFailed", unavailable.DownloadEvidence.Reason);
        Assert.Equal(MarketSourceAvailability.Unavailable, unavailable.Observation.Availability);

        async Task RecordAndWait(MarketDataDownloadOutcome outcome)
        {
            await commands.RecordAsync(outcome);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var partition = new MarketDataDownloadPartition(outcome.Dataset, "FMP", outcome.Scope, outcome.ValueDate);
            while (true)
            {
                var result = await queries.GetAttemptAsync(partition, new(outcome.RequestedAtUtc, outcome.ImportCommandId), deadline.Token);
                Assert.True(result.Success, result.ErrorMessage);
                if (result.Value!.Found) break;
                await Task.Delay(50, deadline.Token);
            }
        }
    }

    static MarketDataDownloadOutcome Outcome(DateOnly date, string scope = "ALL", MarketDataDownloadDataset dataset = MarketDataDownloadDataset.EconomicCalendar)
    {
        var finished = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow.AddMilliseconds(-10));
        return new()
        {
            Dataset = dataset, Scope = scope, ValueDate = date,
            ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(),
            RequestedAtUtc = finished.AddMilliseconds(-2), StartedAtUtc = finished.AddMilliseconds(-1), FinishedAtUtc = finished,
            Status = MarketDataDownloadStatus.Completed, DownloadedRecordCount = 0, PersistedRecordCount = 0, ElapsedMilliseconds = 1
        };
    }

    public async Task DisposeAsync()
    {
        if (host is not null) await host.DisposeAsync();
        if (root is not null) await root.DisposeAsync();
        fixture?.Dispose();
        if (session is not null)
        {
            // This identifier is generated exclusively by this fixture, never supplied externally.
            await session.ExecuteAsync(new SimpleStatement($"DROP KEYSPACE IF EXISTS {keyspace};"));
            session.Dispose();
        }
        cluster?.Dispose();
    }
}
