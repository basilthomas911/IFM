using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Domain.Reference.Repository;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure","SelfContained")]
public sealed class PostgresInstrumentDefinitionSelectionTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Selection_pages_are_bounded_snapshot_pinned_and_reject_cross_filter_cursors()
    {
        await using var isolated = await PostgresInstrumentTestDatabase.CreateAsync(fixture.Connection);
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings().Add("test", isolated.Connection, "System.Data.Postgres")
            .Add("ReferenceDbConnection", isolated.Connection, "System.Data.Postgres");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        await new ReferenceSchemaDb(settings, logger).CreateAllAsync();
        try
        {
            var db = new Db(settings["test"], logger);
            var snapshot = Guid.NewGuid();
            var store = new InstrumentDefinitionStore(new ReferenceDbContext(settings,Substitute.For<IDbContextFactory>(),Substitute.For<TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator>(),logger), Substitute.For<ITradeStrategySymbolStore>());
            for (uint id = 1; id <= 5; ++id)
                await store.IndexSelectionAsync(new()
                {
                    SnapshotId = snapshot,
                    Dataset = "GLBX.MDP3",
                    Root = "ES",
                    InstrumentClass = "C",
                    PublisherId = 1,
                    InstrumentId = id,
                    RawSymbol = "fixture/" + id,
                    ExpirationUtc = DateTimeOffset.UtcNow.AddDays(10),
                    DefinitionDigest = new('a', 64),
                    Strike = 6500 + id
                }, token);
            await db.Use("Selection.Snapshot", $"""
                INSERT INTO reference.instrument_definition_snapshot(catalog,snapshot_id,completed_utc,record_count,datasets_json)
                VALUES('current','{snapshot}','2026-09-19T00:00:00Z',5,'["GLBX.MDP3"]');
                """).ExecuteCommandAsync(token);
            var request = new InstrumentDefinitionPageRequest { Root = "ES", Options = true, PageSize = 2 };
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetSelectionPageAsync(request, DateTimeOffset.UtcNow, token));
            await db.Use("Selection.Complete", $"INSERT INTO reference.instrument_definition_selection_status(snapshot_id,complete) VALUES('{snapshot}',true);").ExecuteCommandAsync(token);
            var first = await store.GetSelectionPageAsync(request, DateTimeOffset.UtcNow, token);
            Assert.Equal(new uint[] { 1, 2 }, first.Items.Select(x => x.InstrumentId));
            var next = request with { SnapshotId = snapshot, ContinuationToken = first.ContinuationToken };
            var second = await store.GetSelectionPageAsync(next, DateTimeOffset.UtcNow, token);
            Assert.Equal(new uint[] { 3, 4 }, second.Items.Select(x => x.InstrumentId));
            var last = await store.GetSelectionPageAsync(next with { ContinuationToken = second.ContinuationToken }, DateTimeOffset.UtcNow, token);
            Assert.Equal(5u, Assert.Single(last.Items).InstrumentId);
            Assert.Null(last.ContinuationToken);
            await Assert.ThrowsAsync<ArgumentException>(() => store.GetSelectionPageAsync(next with { Root = "NQ" }, DateTimeOffset.UtcNow, token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetSelectionPageAsync(next with { SnapshotId = Guid.NewGuid() }, DateTimeOffset.UtcNow, token));
        }
        finally
        {
            // The isolated database owner disposes only this test database.
        }
    }
    sealed class Db(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Db>(setting, logger)
    { public override IObjectRepository Database => this; }
}
