using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NATS.Net;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.LogDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests;

/// <summary>Dedicated brokers on 24222/26379/25432; unique Scylla keyspace. Never enables live feeds.</summary>
public sealed class Stage2DefinitionRuntimeTests
{
    [Fact]
    public async Task Migrated_reference_actor_serves_snapshot_pinned_HTTP_and_NATS_pages()
    {
        const string pg = "Host=127.0.0.1;Port=25432;Database=ifm_stage2";
        var keyspace = "ifm_s2_" + Guid.NewGuid().ToString("N");
        var referenceRoot = "ES" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var scylla = $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}";
        var logger = NullLogger<DbProvider>.Instance;
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add("ReferenceDbConnection", scylla, "System.Data.ScyllaDb")
            .Add("SecuritiesDbConnection", scylla, "System.Data.ScyllaDb")
            .Add("ConfigurationDbConnection", pg, "System.Data.Postgres")
            .Add("EventSourceActorDbConnection", pg, "System.Data.Postgres")
            .Add("LogDbConnection", pg, "System.Data.Postgres")
            .Add("SequenceIdDbConnection", pg, "System.Data.Postgres");
        var admin = new Db(settings["admin"], logger);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        await admin.Use("Stage2.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            await using var nats = new NatsClient("nats://127.0.0.1:24222");
            await nats.CreateJetStreamContext().CreateOrUpdateStreamAsync(new StreamConfig("Stage2Events", ["Event.>"]));
            await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
            await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
            await new LogSchemaDb(settings, logger).CreateAllAsync();
            await new SequenceIdSchemaDb(settings, logger).CreateAllAsync();
            await new ReferenceSchemaDb(settings, logger).CreateAllAsync();
            await new SecuritiesSchemaDb(settings, logger).CreateAllAsync();
            await new SecuritiesSchemaDb(settings, logger).CreateAllAsync();
            var db = new Db(settings["ReferenceDbConnection"], logger);
            var store = new InstrumentDefinitionStore(db, Substitute.For<ITradeStrategySymbolStore>());
            var snapshot = Guid.NewGuid();
            for (uint id = 1; id <= 3; id++)
                await store.IndexSelectionAsync(new()
                {
                    SnapshotId = snapshot, Dataset = "GLBX.MDP3", Root = "ES", InstrumentClass = "C",
                    PublisherId = 1, InstrumentId = id, RawSymbol = $"ES-test-{id}", Strike = 6500.5m + id,
                    ExpirationUtc = DateTimeOffset.UtcNow.AddDays(10), DefinitionDigest = new('a', 64)
                }, token);
            await db.Use("Stage2.Snapshot", $"INSERT INTO instrument_definition_snapshot(catalog,snapshot_id,completed_utc,record_count,datasets_json) VALUES('current',{snapshot},'2026-09-19T00:00:00Z',3,'[\"GLBX.MDP3\"]');").ExecuteCommandAsync(token);
            await db.Use("Stage2.Complete", $"INSERT INTO instrument_definition_selection_status(snapshot_id,complete) VALUES({snapshot},true);").ExecuteCommandAsync(token);
            await using var source = new WebApplicationFactory<Program>();
            await using var host = source.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development")
                    .UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.MarketData.Securities")
                    .UseSetting("IFM_TEST_NATS_URL", "nats://127.0.0.1:24222")
                    .UseSetting("IFM_TEST_REDIS_URL", "127.0.0.1:26379")
                    .UseSetting("IFM_TEST_POSTGRES_CONNECTION", pg);
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new[] { "Securities", "Reference", "Trade", "Fund", "OptionPricer", "MarketData" }
                        .Select(name => new KeyValuePair<string, string?>($"ConnectionStrings:{name}DbConnection", scylla))));
                // This acceptance host runs only reference actors, never trading background services.
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                    var lookups = Substitute.For<IReferenceLookupService>();
                    lookups.SymbolExists(referenceRoot).Returns(true);
                    lookups.CurrencyExists("USD").Returns(true);
                    lookups.ExchangeExists("CME").Returns(true);
                    lookups.MultiplierExists("50").Returns(true);
                    services.AddSingleton(lookups);
                });
            });
            using var http = host.CreateClient();
            var producer = host.Services.GetRequiredService<IActorProducer>();
            await producer.StartAsync(new ActorMailboxId(ActorType.Query, "Stage2Acceptance"));
            try
            {
                var api = new MarketDataQueryApi(producer);
                var request = new InstrumentDefinitionPageRequest { Root = "ES", Options = true, PageSize = 2 };
                var first = await api.GetInstrumentDefinitionsAsync(request, token);
                Assert.True(first.Success, first.ErrorMessage);
                Assert.Equal(new uint[] { 1, 2 }, first.Value!.Items.Select(x => x.InstrumentId));
                Assert.Equal(6501.5m, first.Value.Items[0].Strike);
                var next = request with { SnapshotId = snapshot, ContinuationToken = first.Value.ContinuationToken };
                using var response = await http.GetAsync("/api/marketdata/instrument-definitions?" + next.QueryParams, token);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(token);
                using var body = System.Text.Json.JsonDocument.Parse(json);
                Assert.Contains("ES-test-3", json);
                Assert.DoesNotContain("ES-test-1", json);
                var wrong = await api.GetInstrumentDefinitionsAsync(next with { Root = "NQ" }, token);
                Assert.False(wrong.Success);
                var stale = await api.GetInstrumentDefinitionsAsync(next with { SnapshotId = Guid.NewGuid() }, token);
                Assert.False(stale.Success);
                // Given provider-selected references, command/event projection and subsequent queries preserve identity.
                var at = DateTimeOffset.UtcNow;
                var definition = new InstrumentDefinitionSelection
                {
                    SnapshotId = snapshot, Dataset = "GLBX.MDP3", Root = referenceRoot, PublisherId = 1, InstrumentId = 99,
                    InstrumentClass = "F", RawSymbol = "ESZ6-fixture", Currency = "USD", Exchange = "CME",
                    ExpirationUtc = new DateTimeOffset(2026, 12, 18, 21, 0, 0, TimeSpan.Zero),
                    DefinitionTimestampUtc = at, DefinitionDigest = new('b', 64), RawDefinitionReference = "fixture/future",
                    Multiplier = 50, TickSize = .25m
                };
                var future = InstrumentDefinitionImport.Future(definition, "America/New_York", at);
                var commands = new MarketDataCommandApi(producer);
                var added = await commands.AddFuturesContractAsync(future, false);
                Assert.True(added.Success, added.ErrorMessage);
                await Until(async () => (await api.GetFuturesContractAsync(future.ContractId)).Value == future, token);
                var option = InstrumentDefinitionImport.Option(definition with
                {
                    InstrumentId = 42, UnderlyingInstrumentId = 99, InstrumentClass = "C", RawSymbol = "ESZ6 C6500.5",
                    Strike = 6500.5m, RawDefinitionReference = "fixture/option"
                }, future, "America/New_York", at);
                var optionAdded = await commands.AddFuturesOptionContractAsync(option, false);
                Assert.True(optionAdded.Success, optionAdded.ErrorMessage);
                await Until(async () => (await api.GetFuturesOptionContractAsync(option.ContractId)).Value == option, token);
                var changed = option with { Description = "Changed through NATS" };
                var optionChanged = await commands.ChangeFuturesOptionContractAsync(option.ContractId, changed, false);
                Assert.True(optionChanged.Success, optionChanged.ErrorMessage);
                await Until(async () => (await api.GetFuturesOptionContractAsync(option.ContractId)).Value == changed, token);
                var changedFuture = future with { Description = "Changed future through NATS" };
                var futureChanged = await commands.ChangeFuturesContractAsync(future.Id, changedFuture, false);
                Assert.True(futureChanged.Success, futureChanged.ErrorMessage);
                await Until(async () => (await api.GetFuturesContractAsync(future.ContractId)).Value == changedFuture, token);
            }
            finally { await producer.StopAsync(); }
        }
        finally
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await admin.Use("Stage2.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
                    break;
                }
                catch when (attempt < 2)
                {
                    // Large schema removal may outlast the driver's request timeout while completing server-side.
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
    sealed class Db(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Db>(setting, logger)
    { public override IObjectRepository Database => this; }
    static async Task Until(Func<Task<bool>> condition, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        while (!await condition()) await Task.Delay(50, deadline.Token);
    }
}
