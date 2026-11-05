using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class HistoricalDataLoadPersistenceTests
{
    [Fact]
    public async Task Context_and_adapter_preserve_resume_state_immutable_manifests_and_atomic_failure()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("historical-load-context");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance)
            .CreateAsync(new[] { "historical_data_loader", "historical_analytics_attempt_projection" });
        var factory = Substitute.For<IDbContextFactory>();
        var db = new MarketDataDbContext(settings, factory, Substitute.For<IBlackboardService>(),
            Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        factory.MarketDataDb.Returns(db);
        IHistoricalDataLoaderStore adapter = new HistoricalDataLoaderStore(factory);
        var attempt = Guid.NewGuid();
        var time = new DateTimeOffset(2026,10,12,15,0,0,TimeSpan.Zero);
        var state = new HistoricalDataLoaderState
        {
            DataLoadAttemptId = attempt, RequestSha256 = "historical-request", Status = HistoricalDataLoaderStatus.Processing,
            Checkpoint = new HistoricalAcquisitionCheckpoint { DataLoadAttemptId = attempt,
                Stage = HistoricalAcquisitionStage.Downloaded, ProviderJobId = "job", ProviderFileId = "file",
                BatchOrdinal = 1, SourcePosition = "resume:1" }, UpdatedAtUtc = time
        };
        Assert.Null(await adapter.GetAsync(attempt, default));
        await adapter.SaveAsync(state, default);
        (await db.GetHistoricalDataLoadAsync(attempt)).Should().BeEquivalentTo(state);
        Assert.Null(await adapter.GetCompletedByRequestHashAsync(state.RequestSha256, default));
        var manifest = new MarketDataHistoricalManifest { ManifestId = Guid.NewGuid(), DataLoadAttemptId = attempt,
            ProviderJobId = "job", RequestSha256 = state.RequestSha256, NormalizedSha256 = "accepted",
            ObservationCount = 12, FirstValueDate = new(2026,10,9), LastValueDate = new(2026,10,12), CompletedAtUtc = time };
        var completed = state with { Status = HistoricalDataLoaderStatus.Completed,
            Checkpoint = state.Checkpoint with { Stage = HistoricalAcquisitionStage.Completed, BatchOrdinal = 2 },
            Manifest = manifest, Audit = new HistoricalDataLoaderAudit(2, [], []) };
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(infrastructure.PostgresConnectionString);
        await connection.OpenAsync();
        await using (var constraint = new NpgsqlCommand("ALTER TABLE historical_data_load_manifest ADD CONSTRAINT reject_manifest_probe CHECK(manifest_json NOT LIKE '%REJECT%');", connection))
            await constraint.ExecuteNonQueryAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveHistoricalDataLoadAsync(completed with
            { Manifest = manifest with { NormalizedSha256 = "REJECT" } }));
        (await adapter.GetAsync(attempt, default)).Should().BeEquivalentTo(state);
        Assert.Null(await db.GetCompletedHistoricalDataLoadAsync(state.RequestSha256));
        await adapter.SaveAsync(completed, default);
        (await adapter.GetCompletedByRequestHashAsync(state.RequestSha256, default)).Should().BeEquivalentTo(completed);
        await adapter.SaveAsync(completed with { Manifest = manifest with { NormalizedSha256 = "changed" } }, default);
        Assert.Equal("accepted", (await adapter.GetAsync(attempt, default))!.Manifest!.NormalizedSha256);
        Assert.Equal("Completed", (await db.GetHistoricalAnalyticsAttemptProjectionAsync(attempt))?.Status);
        var duplicate = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveHistoricalDataLoadAsync(completed with
            { DataLoadAttemptId = duplicate, Checkpoint = completed.Checkpoint with { DataLoadAttemptId = duplicate } }));
        Assert.Null(await db.GetHistoricalDataLoadAsync(duplicate));
        var id = new FuturesAnalyticsHistoricalDataLoaderEntityId(duplicate);
        var requested = new FuturesAnalyticsHistoricalDataLoaderRequestedEvent
        {
            EntityId = id, EventId = 1, ReceivedOn = time.UtcDateTime
        };
        var reconciled = new FuturesAnalyticsHistoricalDataLoaderCompletedEvent
        {
            EntityId = id, EventId = 2, RequestSha256 = state.RequestSha256,
            ManifestId = manifest.ManifestId, LastCompletedBatchOrdinal = 2, CompletedAtUtc = time.UtcDateTime
        };
        var projector = new FuturesAnalyticsHistoricalDataLoaderEventProjector(
            factory, Substitute.For<IDurableReplayQueue>(), Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(), NullLogger<FuturesAnalyticsHistoricalDataLoaderEventProjector>.Instance);
        var rebuilder = new ReadProjectionRebuilder(projector.ProjectionDescriptors);
        Assert.Equal(2, await rebuilder.RebuildAsync(Events(requested, reconciled)));
        Assert.Equal(1, await rebuilder.RebuildAsync(Events(requested)));
        var diagnostics = await db.GetHistoricalAnalyticsAttemptProjectionAsync(duplicate);
        Assert.NotNull(diagnostics);
        Assert.Equal("Completed", diagnostics.Status);
        Assert.Equal(manifest.ManifestId, diagnostics.ManifestId);
        Assert.Equal(state.RequestSha256, diagnostics.RequestSha256);
        Assert.Equal(2, diagnostics.LastCompletedBatchOrdinal);
        Assert.Null(await db.GetHistoricalDataLoadAsync(duplicate));
        var warmupId = Guid.NewGuid();
        var warmup = completed with
        {
            DataLoadAttemptId = warmupId,
            RequestSha256 = $"automatic:{warmupId:D}",
            Checkpoint = completed.Checkpoint with { DataLoadAttemptId = warmupId },
            Manifest = null
        };
        await adapter.SaveAsync(warmup, default);
        (await adapter.GetAsync(warmupId, default)).Should().BeEquivalentTo(warmup);
        Assert.Null(await adapter.GetCompletedByRequestHashAsync(warmup.RequestSha256, default));
        await Assert.ThrowsAsync<ArgumentException>(() => db.GetCompletedHistoricalDataLoadAsync(" "));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.GetHistoricalDataLoadAsync(attempt, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.SaveHistoricalDataLoadAsync(state, cancelled.Token));
    }

    static async IAsyncEnumerable<IEvent> Events(params IEvent[] events)
    {
        await Task.CompletedTask;
        foreach (var fact in events)
            yield return fact;
    }
}
