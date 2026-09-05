using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

public sealed class InstrumentDefinitionScyllaTests
{
    [Fact]
    public async Task Exact_definitions_roundtrip_and_published_products_are_restart_safe()
    {
        var keyspace = "ifm_defs_" + Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings().Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add(ReferenceDbContext.ReferenceDbConnection, $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new Admin(settings["admin"], logger);
        await admin.Use("DefinitionsTest.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            await new ReferenceSchemaDb(settings, logger).CreateAsync(["instrument_definition", "instrument_definition_product", "instrument_definition_snapshot", "trade_strategy_symbol_v1"], token);
            long next = 0;
            var ids = Substitute.For<ISequenceIdGenerator>();
            ids.GetSequenceIdAsync(Arg.Any<SequenceName>(), Arg.Any<CancellationToken>()).Returns(_ => new ValueTask<long>(Interlocked.Increment(ref next)));
            ReferenceDbContext Context()
            {
                var objects = new Dictionary<Type, object>();
                var factory = new DbContextFactory(new DbContextResolver(type => objects[type]));
                var context = new ReferenceDbContext(settings, factory, ids, logger);
                objects.Add(typeof(IObjectRepository<ReferenceDbContext>), context);
                return context;
            }
            var db = Context(); var store = db.InstrumentDefinitions;
            var row = ExactInstrumentDefinition.Parse("GLBX.MDP3", """
                {"hd":{"rtype":19,"publisher_id":1,"instrument_id":42002230,"ts_event":"1788089407203111341"},"ts_recv":"1788480000000000000","raw_symbol":"OZRU7 P2020","asset":"OZR","instrument_class":"P","underlying_id":42000681,"currency":"USD","exchange":"XCBT","expiration":"1819390800000000000","activation":"1784928600000000000","strike_price":"2020000000000","high_limit_price":"9223372036854775807","unknown_future_field":{"x":"keep exactly"}}
                """);
            var first = new InstrumentDefinitionSnapshot(Guid.NewGuid(), DateTime.UtcNow, 1, ["GLBX.MDP3"]);
            await store.InsertAsync(first.Id, 0, row, token);
            Assert.Null(await store.GetSnapshotAsync(token));
            var product = new TradeStrategyProduct(TradeStrategyFamilyType.FuturesOption, "ZR", "USD", "XCBT");
            await store.PublishAsync(first, [product], token);
            var restarted = Context().InstrumentDefinitions;
            Assert.Equal(first.Id, (await restarted.GetSnapshotAsync(token))!.Id);
            var catalog = new StoredInstrumentDefinitionSymbolCatalog(restarted);
            var symbols = await catalog.GetAsync(TradeStrategyFamilyType.FuturesOption, token);
            Assert.True(symbols.Success);
            var symbol = Assert.Single(symbols.Value!);
            Assert.Equal("ZR", symbol.Symbol); Assert.Equal("XCBT", symbol.Exchange); Assert.Equal("USD", symbol.Currency);
            var raw = new List<string>();
            await foreach (var json in restarted.ReadJsonAsync(first.Id, "GLBX.MDP3", 0, token)) raw.Add(json);
            Assert.Equal(row.Json, Assert.Single(raw));
            Assert.Empty((await catalog.GetAsync(TradeStrategyFamilyType.Futures, token)).Value!);
            var second = first with { Id = Guid.NewGuid() };
            await store.InsertAsync(second.Id, 128, row, token);
            Assert.Equal(first.Id, (await restarted.GetSnapshotAsync(token))!.Id);
            await store.PublishAsync(second, [product, product with { Family = TradeStrategyFamilyType.Futures }], token);
            Assert.Equal(second.Id, (await restarted.GetSnapshotAsync(token))!.Id);
            Assert.Equal(symbol.Id, Assert.Single((await catalog.GetAsync(TradeStrategyFamilyType.FuturesOption, token)).Value!).Id);
            Assert.Single((await catalog.GetAsync(TradeStrategyFamilyType.Futures, token)).Value!);
        }
        finally
        {
            // This identifier is generated by this test; never an application keyspace.
            await admin.Use("DefinitionsTest.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }
    }
    sealed class Admin(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Admin>(setting, logger)
    { public override IObjectRepository Database => this; }
}
