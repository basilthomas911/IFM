using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
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
            .CreateAsync(new[] { "historical_data_loader" });
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
        var duplicate = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveHistoricalDataLoadAsync(completed with
            { DataLoadAttemptId = duplicate, Checkpoint = completed.Checkpoint with { DataLoadAttemptId = duplicate } }));
        Assert.Null(await db.GetHistoricalDataLoadAsync(duplicate));
        await Assert.ThrowsAsync<ArgumentException>(() => db.GetCompletedHistoricalDataLoadAsync(" "));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.GetHistoricalDataLoadAsync(attempt, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.SaveHistoricalDataLoadAsync(state, cancelled.Token));
    }
}
