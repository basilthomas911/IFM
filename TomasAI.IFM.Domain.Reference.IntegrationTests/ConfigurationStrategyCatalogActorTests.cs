using TomasAI.IFM.Domain.Reference.Repository;
using System.Reflection;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.TradeStrategyFamilies;
using TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Actor;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using Npgsql;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

[Collection(ReferenceIntegrationInfrastructureCollection.Name)]
public sealed class ConfigurationStrategyCatalogActorTests(ReferenceIntegrationInfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Catalog_actor_commits_the_source_fact_on_the_live_transport()
    {
        var client = new ReferenceCommandApi(infrastructure.ActorProducer);
        var key = new CatalogKey(StrategyCatalogKind.Family, Guid.NewGuid(), 1);
        var request = new CatalogCommandRequest(Guid.NewGuid(), CatalogCommandOperation.SaveDraft,
            new StrategyCatalogDefinition { Key = key, Code = $"Source-{key.Id:N}", Name = "Catalog source" });
        var result = await client.ExecuteStrategyCatalogAsync(request);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(await infrastructure.ActorEventSourceDb.HasEventForCommandAsync(request.OperationId));

        var rejectedId = Guid.NewGuid();
        var rejected = await client.ExecuteStrategyCatalogAsync(new CatalogCommandRequest(rejectedId,
            CatalogCommandOperation.Publish, Key: key, ExpectedHash: new string('0', 64), EffectiveUtc: DateTime.UtcNow));
        Assert.False(rejected.Success);
        Assert.False(await infrastructure.ActorEventSourceDb.HasEventForCommandAsync(rejectedId));
    }

    [Fact]
    public async Task Legacy_catalog_route_uses_the_same_source_stream_and_projector()
    {
        var key = new CatalogKey(StrategyCatalogKind.Family, Guid.NewGuid(), 1);
        var request = new CatalogCommandRequest(Guid.NewGuid(), CatalogCommandOperation.SaveDraft,
            new StrategyCatalogDefinition { Key = key, Code = $"Compatible-{key.Id:N}", Name = "Compatible catalog" });
        var command = new StrategyCatalogCommand
        {
            CommandId = request.OperationId,
            RequestJson = StrategyCatalogJson.Write(request),
            Subject = new(ActorType.Command, StrategyCatalogCommand.Actor, StrategyCatalogCommand.Verb, ActorEntityId.Default.Format())
        };
        var result = await infrastructure.ActorProducer.RequestAsync<StrategyCatalogCommand, ActorEntityId, GuidResult>(
            command.Subject, command, command.EntityId, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(await infrastructure.ActorEventSourceDb.HasEventForCommandAsync(command.CommandId));
        Assert.NotNull(await infrastructure.ConfigurationDb.GetStrategyCatalogAsync(key));
    }

    [Fact]
    public async Task Committed_catalog_fact_can_project_after_the_accepting_process_stops()
    {
        var database = infrastructure.ConfigurationDb;
        var key = new CatalogKey(StrategyCatalogKind.Family, Guid.NewGuid(), 1);
        var request = new CatalogCommandRequest(Guid.NewGuid(), CatalogCommandOperation.SaveDraft,
            new StrategyCatalogDefinition { Key = key, Code = $"Recover-{key.Id:N}", Name = "Catalog recovery" });
        var source = await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            database.RecordStrategyCatalogTransitionAsync(transaction, request, "integration", token));
        Assert.NotNull(source);
        Assert.Null(await database.GetStrategyCatalogAsync(key));
        Assert.Null(await database.GetProjectedParameterRevisionAsync());
        Assert.NotNull(await infrastructure.ActorEventSourceDb.GetEventProjectorExecutionStateAsync(
            source.EventId, "StrategyCatalogEventProjector"));

        Assert.True(await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            database.ProjectStrategyCatalogSourceAsync(transaction, source, false, token)));
        Assert.False(await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            database.ProjectStrategyCatalogSourceAsync(transaction, source, false, token)));
        Assert.Equal(source.AfterJson, StrategyCatalogJson.Write(await database.GetStrategyCatalogAsync(key)));
        Assert.NotNull(await database.GetProjectedParameterRevisionAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Serialized_catalog_commands_persist_publish_and_retire_exact_versions_in_postgres()
    {
        var context = infrastructure.ConfigurationDb;
        var client = new ReferenceCommandApi(infrastructure.ActorProducer);
        var definition = new StrategyCatalogDefinition { Key = new(StrategyCatalogKind.Family, Guid.NewGuid(), 1), Code = "ActorCatalog-" + Guid.NewGuid().ToString("N"), Name = "Catalog integration" };
        var save = new CatalogCommandRequest(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, definition);
        await Send(save); await Send(save);
        await Send(save with { OperationId = Guid.NewGuid() });
        var row = (await context.GetStrategyCatalogAsync(definition.Key))!;
        row.Status.Should().Be(CatalogLifecycleStatus.Draft);
        var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        await Send(new(Guid.NewGuid(), CatalogCommandOperation.Publish, Key: definition.Key, ExpectedHash: row.ContentHash, EffectiveUtc: now));
        (await context.GetStrategyCatalogAsync(definition.Key))!.Status.Should().Be(CatalogLifecycleStatus.Published);
        var second = definition with { Key = definition.Key with { Version = 2 }, Description = "Editable revision" };
        await Send(new(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, second, ExpectedPreviousVersion: 1));
        await Send(new(Guid.NewGuid(), CatalogCommandOperation.Retire, Key: definition.Key, ExpectedHash: row.ContentHash, EffectiveUtc: now.AddSeconds(1)));
        (await context.GetStrategyCatalogAsync(definition.Key))!.Status.Should().Be(CatalogLifecycleStatus.Retired);
        (await context.GetStrategyCatalogAsync(second.Key))!.Status.Should().Be(CatalogLifecycleStatus.Draft);
        var listed = await context.GetStrategyCatalogsAsync(StrategyCatalogKind.Family, 128);
        listed.Where(x => x.Key.Id == definition.Key.Id).Should().ContainSingle().Which.Key.Version.Should().Be(2);
        var stream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(
            $"StrategyCatalog.{(int)definition.Key.Kind}-{definition.Key.Id:N}-1");
        var history = await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<StrategyCatalogSourceEvent>(stream);
        var head = history.Last();
        var fact = Assert.IsType<StrategyCatalogSourceEvent>(
            EventLogMessagePackCodec.Shared.Deserialize(head.EventTypeName, head.EventVersion, head.EventData));
        Assert.Equal(CatalogLifecycleStatus.Retired,
            StrategyCatalogJson.Read<StoredStrategyCatalogDefinition>(fact.AfterJson).Status);
        Assert.Equal(3, await infrastructure.ActorEventSourceDb.GetCommittedStreamVersionAsync(stream));
        var secondStream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(
            $"StrategyCatalog.{StrategyCatalogLifecycleCommand.Address(second.Key).Format()}");
        var secondEvent = Assert.Single(await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<StrategyCatalogSourceEvent>(secondStream));
        var secondFact = Assert.IsType<StrategyCatalogSourceEvent>(
            EventLogMessagePackCodec.Shared.Deserialize(secondEvent.EventTypeName, secondEvent.EventVersion, secondEvent.EventData));

        var legacy = definition with
        {
            Key = definition.Key with { Id = Guid.NewGuid() },
            Code = $"Legacy-{Guid.NewGuid():N}"
        };
        await context.InsertStrategyCatalogDraftAsync(legacy, 0, "legacy fixture");
        await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            context.CaptureStrategyCatalogBaselineAsync(transaction, token));
        var legacyStream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(
            $"StrategyCatalog.{StrategyCatalogLifecycleCommand.Address(legacy.Key).Format()}");
        var legacyEvent = Assert.Single(await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<StrategyCatalogSourceEvent>(legacyStream));
        var legacyFact = Assert.IsType<StrategyCatalogSourceEvent>(
            EventLogMessagePackCodec.Shared.Deserialize(legacyEvent.EventTypeName, legacyEvent.EventVersion, legacyEvent.EventData));
        Assert.True(legacyFact.IsBaseline);
        var sourceHeads = await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            context.GetStrategyCatalogSourceHeadsAsync(transaction, token));

        // The latest fact contains the accepted state and its predecessor, so it can rebuild
        // an empty projection without consulting the original ConfigurationDb rows.
        var rebuildDatabase = $"catalog_rebuild_{Guid.NewGuid():N}";
        await using (var admin = new TomasAI.IFM.Framework.Storage.Postgres.PostgresObjectDataRepositoryConnection()
            .As<NpgsqlConnection>(infrastructure.PostgresConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {rebuildDatabase}", admin);
            await create.ExecuteNonQueryAsync();
        }
        var rebuildConnection = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Database = rebuildDatabase }.ConnectionString;
        var settings = new DbConnectionSettings()
            .Add(ConfigurationDbContext.ConfigurationDbConnection, rebuildConnection, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, rebuildConnection, "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
        await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
        var factory = Substitute.For<IDbContextFactory>();
        var rebuilt = new ConfigurationDbContext(settings, factory, logger);
        factory.ConfigurationDb.Returns(rebuilt);
        var rebuildTransactions = new PostgresEventTransaction(settings);
        var rebuildService = new StrategyCatalogService(factory, rebuildTransactions);
        Assert.Null(await rebuilt.GetStrategyCatalogAsync(definition.Key));
        await rebuildTransactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await rebuilt.RebuildStrategyCatalogAsync(transaction, sourceHeads.Reverse().ToArray(), token);
            return true;
        });
        Assert.False(await rebuildService.ProjectAsync(fact, true, CancellationToken.None));
        Assert.Equal(fact.AfterJson, StrategyCatalogJson.Write(await rebuilt.GetStrategyCatalogAsync(definition.Key)));
        Assert.Equal(secondFact.AfterJson, StrategyCatalogJson.Write(await rebuilt.GetStrategyCatalogAsync(second.Key)));
        Assert.Equal(legacyFact.AfterJson, StrategyCatalogJson.Write(await rebuilt.GetStrategyCatalogAsync(legacy.Key)));
        foreach (var seeded in StrategyCatalogDefaults.Create())
            Assert.Equal(StrategyCatalogJson.Write(await context.GetStrategyCatalogAsync(seeded.Key)),
                StrategyCatalogJson.Write(await rebuilt.GetStrategyCatalogAsync(seeded.Key)));

        async Task Send(CatalogCommandRequest request)
        {
            var result = await client.ExecuteStrategyCatalogAsync(request);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(request.OperationId, result.Value);
        }
    }
}
