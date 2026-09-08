using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

public sealed class CompositionPreparationScyllaTests
{
    [Fact]
    public async Task Concurrent_captures_choose_one_immutable_winner_and_replay_after_repository_restart()
    {
        var keyspace = "ifm_ocp_" + Guid.NewGuid().ToString("N");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add("test", $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new Repository(settings["admin"], logger);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = timeout.Token;
        await admin.Use("OcpPreparation.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            var db = new Repository(settings["test"], logger);
            await db.Use("OcpPreparation.Schema", CompositionPreparationStore.CreateTable).ExecuteCommandAsync(token);
            var store = new CompositionPreparationStore(db);
            var other = new CompositionPreparationStore(new Repository(settings["test"], logger));
            var key = new CompositionPreparationKey(Guid.NewGuid(), 5, new string('a', 64));
            Assert.Null(await store.ReadAsync(key, token));
            var a = Prepared(key); var b = Prepared(key);
            var winners = await Task.WhenAll(store.CommitAsync(a, token), other.CommitAsync(b, token));
            Assert.Equal(winners[0].Digest, winners[1].Digest);
            Assert.Contains(winners[0].Digest, new[] { a.Digest, b.Digest });
            var restarted = new CompositionPreparationStore(new Repository(settings["test"], logger));
            Assert.Equal(winners[0].Digest, (await restarted.ReadAsync(key, token))!.Digest);
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.ReadAsync(key with { InputSha256 = new('b', 64) }, token));
            // A different market capture cannot overwrite the first snapshot even after restart.
            Assert.Equal(winners[0].Digest, (await restarted.CommitAsync(Prepared(key), token)).Digest);
            Assert.Null(await restarted.ReadAsync(key with { InputRevision = 6 }, token));
        }
        finally { await admin.Use("OcpPreparation.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None); }
    }

    static CompositionPreparation Prepared(CompositionPreparationKey key)
    {
        var at = DateTimeOffset.UtcNow;
        var request = new CompositionSnapshotRequest(Guid.NewGuid(), "complete-empty", "Daily", Guid.NewGuid(), at, at.AddSeconds(10), true);
        var snapshot = new MarketCompositionSnapshot(1, request.SnapshotId, request.ScopeId, "fixture/v1", request.Horizon,
            request.GenerationId, at, at.AddSeconds(5), [], "");
        snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
        var value = new CompositionPreparation(1, key, "GLBX.MDP3", request, snapshot, at, "");
        return value with { Digest = PricingSemanticHash.Compute(value) };
    }
    sealed class Repository(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Repository>(setting, logger)
    { public override IObjectRepository Database => this; }
}
