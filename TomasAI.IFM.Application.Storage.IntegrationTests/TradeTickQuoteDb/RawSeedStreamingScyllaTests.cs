using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeTickQuoteDb;

public sealed class RawSeedStreamingScyllaTests
{
    [Fact]
    public async Task Raw_seed_stream_preserves_all_rows_across_pages_and_can_restart_after_cancellation()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("apiseedstream");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(TradeTickQuoteDbContext.TradeTickQuoteDbConnection,
            infrastructure.CqlConnectionString, "System.Data.ScyllaDb");
        await new TradeTickQuoteSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var db = new TradeTickQuoteDbContext(settings, NullLogger<DbProvider>.Instance);
        var date = new DateOnly(2026, 11, 6);
        var contract = "ES-stream-" + Guid.NewGuid().ToString("N");
        var entity = new TickDataEntityId(contract, date, AssetTypeId.Futures);
        var start = new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);
        const int count = 6001;
        await Parallel.ForEachAsync(Enumerable.Range(1, count), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (index, _) =>
        {
            var at = start.AddMilliseconds(index);
            var ns = (at.Ticks - DateTime.UnixEpoch.Ticks) * 100;
            await db.InsertTickTradeDataAsync(new FuturesTickTradeDataInsertedEvent
            {
                Subject = new(ActorType.Event, FuturesTickTradeDataInsertedEvent.Actor, FuturesTickTradeDataInsertedEvent.Verb, entity.Format()),
                Id = Guid.NewGuid(), EntityId = entity, CommandId = Guid.NewGuid(), AggregateId = entity.Format(),
                EventSource = "isolated-api-performance", ReceivedOn = at,
                TickDataId = new(contract, date, index, at), AssetTypeId = AssetTypeId.Futures,
                Dataset = "GLBX.MDP3", DefinitionDate = date, PublisherId = 1, InstrumentId = 42,
                TradeData = new((uint)index, ns, ns, 0, 5000000000000, 5000m, 1, (byte)'T', 0, 0)
            });
        });
        var from = TimeOnly.FromDateTime(start);
        var through = TimeOnly.FromDateTime(start.AddSeconds(7));
        var rows = new List<BootstrapTradeRow>();
        await foreach (var row in db.StreamRawFuturesTradeWindowAsync(contract, date, from, through)) rows.Add(row);
        Assert.Equal(count, rows.Count);
        Assert.Equal(Enumerable.Range(1, count).Select(index => (long)index), rows.Select(row => row.Sequence).Order());
        Assert.All(rows, row => { Assert.Equal(5000m, row.Price); Assert.Equal((short)'T', row.Action); Assert.NotNull(row.AggregationTimeUtc); });
        using var cancel = new CancellationTokenSource();
        var observed = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var row in db.StreamRawFuturesTradeWindowAsync(contract, date, from, through, cancel.Token))
                if (++observed == 25) cancel.Cancel();
        });
        Assert.InRange(observed, 25, count - 1);
        var restored = await db.GetRawFuturesTradeWindowAsync(contract, date, from, through);
        Assert.Equal(rows, restored);
    }
}
