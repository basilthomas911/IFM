using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

/// <summary>Runs real Core NATS commands/queries, PostgreSQL state and JetStream projection into Scylla.</summary>
[Trait("Category", "Integration")]
[Collection("DownloadLog runtime")]
public sealed class DownloadLogActorFlowTests(WebApplicationFactory<Program> factory, MarketDataFixture fixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFixture>
{
    [Fact]
    public async Task Startup_import_coordinator_reaches_both_terminal_handlers_and_queryable_logs()
    {
        var reference = Substitute.For<IReferenceDataApi>();
        var calendar = Substitute.For<IEconomicCalendar>(); var treasury = Substitute.For<ITreasuryCurve>();
        reference.EconomicCalendar.Returns(calendar); reference.TreasuryCurve.Returns(treasury);
        var date = new DateOnly(8995, 9, 5);
        calendar.GetAsync(date, date, Arg.Any<IReadOnlySet<string>?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<EconomicCalendarEntry>>([]));
        treasury.GetRangeAsync(date, date, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<TreasuryCurveSnapshot>>([]));
        using var focused = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.MarketData");
            builder.UseSetting("IFM_TEST_NATS_URL", Environment.GetEnvironmentVariable("IFM_DOWNLOADLOG_TEST_NATS_URL") ?? "nats://127.0.0.1:14222");
            builder.ConfigureServices(services => services.AddSingleton(reference));
        });
        var coordinator = new FmpMarketDataImportCoordinator(new MarketDataCommandApi(focused.Services.GetRequiredService<IActorProducer>()),
            new FmpMarketDataImportOptions(), NullLogger<FmpMarketDataImportCoordinator>.Instance);
        var submitted = await coordinator.ImportAsync(new(date, date, CountryCodes: ["US"]));
        Assert.True(submitted.SubmittedCommands == 2, System.Text.Json.JsonSerializer.Serialize(submitted));
        Assert.Equal(0, submitted.RejectedSubmissions);
        var queries = focused.Services.GetRequiredService<IDownloadLogQueryApi>();
        foreach (var attempt in submitted.Dates)
        {
            var dataset = attempt.Dataset == FmpImportDataset.Treasury ? MarketDataDownloadDataset.TreasuryCurve : MarketDataDownloadDataset.EconomicCalendar;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            while (true)
            {
                var reply = await queries.GetStatusAsync(new(dataset, "FMP", "US", date), attempt.CommandId, cancellationToken: timeout.Token);
                Assert.True(reply.Success, reply.ErrorMessage);
                if (reply.Value!.CompletionConfirmed)
                {
                    Assert.Equal(0, reply.Value.SuccessfulAttempt!.Outcome.DownloadedRecordCount);
                    Assert.Equal(0, reply.Value.SuccessfulAttempt.Outcome.PersistedRecordCount);
                    break;
                }
                await Task.Delay(100, timeout.Token);
            }
        }
        await calendar.Received(1).GetAsync(date, date, Arg.Any<IReadOnlySet<string>?>(), Arg.Any<CancellationToken>());
        await treasury.Received(1).GetRangeAsync(date, date, Arg.Any<CancellationToken>());
    }

    [Theory] [InlineData(MarketDataDownloadDataset.EconomicCalendar)] [InlineData(MarketDataDownloadDataset.TreasuryCurve)]
    public async Task Command_projects_and_queries_then_rejects_conflicting_duplicate(MarketDataDownloadDataset dataset)
    {
        using var focused = factory.WithWebHostBuilder(builder => builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.MarketData")
            .UseSetting("IFM_TEST_NATS_URL", Environment.GetEnvironmentVariable("IFM_DOWNLOADLOG_TEST_NATS_URL") ?? "nats://127.0.0.1:14222"));
        var producer = focused.Services.GetRequiredService<IActorProducer>();
        var now = MarketDataDownloadOutcome.MillisecondUtc(DateTime.UtcNow);
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = dataset, Scope = "US", ValueDate = new(8993, 9, 5), ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(),
            RequestedAtUtc = now.AddSeconds(-2), StartedAtUtc = now.AddSeconds(-1), FinishedAtUtc = now,
            Status = MarketDataDownloadStatus.Completed, DownloadedRecordCount = 0, PersistedRecordCount = 0, ElapsedMilliseconds = 1000
        };
        var command = new InsertMarketDataDownloadLogCommand(outcome);
        var reply = await producer.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId, GuidResult>(command.Subject, command, command.EntityId);
        Assert.True(reply.Success, reply.ErrorMessage);
        var queries = new DownloadLogQueryApi(producer);
        var partition = new MarketDataDownloadPartition(dataset, "FMP", "US", outcome.ValueDate);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        MarketDataDownloadStatusResult? status;
        do
        {
            var result = await queries.GetStatusAsync(partition, outcome.ImportCommandId, cancellationToken: timeout.Token);
            Assert.True(result.Success, result.ErrorMessage); status = result.Value;
            if (status!.CompletionConfirmed) break;
            await Task.Delay(100, timeout.Token);
        } while (true);
        Assert.Equal(outcome, status.SuccessfulAttempt!.Outcome);
        var duplicate = await producer.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId, GuidResult>(command.Subject, command, command.EntityId);
        Assert.True(duplicate.Success, duplicate.ErrorMessage);
        var conflicting = new InsertMarketDataDownloadLogCommand(outcome with { DownloadedRecordCount = 9 });
        var conflict = await producer.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId, GuidResult>(conflicting.Subject, conflicting, conflicting.EntityId);
        Assert.False(conflict.Success);
        var exact = await queries.GetAttemptAsync(partition, new(outcome.RequestedAtUtc, outcome.ImportCommandId));
        Assert.True(exact.Success, exact.ErrorMessage); Assert.Equal(outcome, exact.Value!.Attempt!.Outcome);
    }
}
