using System.Buffers;
using TomasAI.IFM.Application.Strategy.StrategyCatalog;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
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
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
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
        var source = await StrategyCatalogService.CommitAsync(database, infrastructure.EventTransactions,
            request, "integration", StrategyCatalogLifecycleCommand.Address(key), TimeProvider.System);
        Assert.NotNull(source);
        Assert.Null(await database.GetStrategyCatalogAsync(key));
        Assert.Null(await database.GetProjectedParameterRevisionAsync());
        Assert.NotNull(await infrastructure.ActorEventSourceDb.GetEventProjectorExecutionStateAsync(
            new EventProjectorIdentity(source.EventId, "StrategyCatalogEventProjector")));

        var settings = new DbConnectionSettings()
            .Add(ConfigurationDbContext.ConfigurationDbConnection, infrastructure.PostgresConnectionString, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, infrastructure.PostgresConnectionString, "System.Data.Postgres");
        var factory = Substitute.For<IDbContextFactory>();
        var restarted = new ConfigurationDbContext(settings, factory, Substitute.For<ILogger<DbProvider>>());
        factory.ConfigurationDb.Returns(restarted);
        var restartedTransactions = new PostgresEventTransaction(settings);
        var restartedService = new StrategyCatalogService(factory, restartedTransactions);
        var streamName = $"StrategyCatalog.{StrategyCatalogLifecycleCommand.Address(key).Format()}";
        var persisted = Assert.IsType<StrategyCatalogSourceEvent>(await restarted.ReadCommittedStrategyCatalogOperationAsync(streamName, request.OperationId));
        Assert.Equal(source.EventId, persisted.EventId);
        Assert.Empty(persisted.AfterJson);
        Assert.NotNull(persisted.AfterVersion);
        Assert.True(await restartedService.ProjectAsync(persisted, false, CancellationToken.None));
        Assert.False(await restartedService.ProjectAsync(persisted, false, CancellationToken.None));
        Assert.Null(await StrategyCatalogService.CommitAsync(restarted, restartedTransactions, request, "integration",
            StrategyCatalogLifecycleCommand.Address(key), TimeProvider.System));
        var streamId = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(streamName);
        Assert.Equal(1, await infrastructure.ActorEventSourceDb.GetCommittedStreamVersionAsync(streamId));
        Assert.Equal(StrategyCatalogJson.Write(source.ReadAfterVersion()), StrategyCatalogJson.Write(await restarted.GetStrategyCatalogAsync(key)));
        Assert.NotNull(await restarted.GetProjectedParameterRevisionAsync());
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
        var stream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(
            $"StrategyCatalog.{StrategyCatalogLifecycleCommand.Address(definition.Key).Format()}");
        var initial = Assert.Single(await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<StrategyCatalogSourceEvent>(stream));
        var creation = EventLogMessagePackCodec.Shared.Deserialize<StrategyCatalogSourceEvent>(initial.EventVersion, initial.EventData);
        Assert.NotNull(creation.AfterVersion);
        Assert.Null(creation.BeforeVersion);
        Assert.Empty(creation.AfterJson);
        var oldBytes = LegacyEventPayload(creation with { AfterVersion = null, AfterJson = StrategyCatalogJson.Write(row) });
        await infrastructure.EventTransactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            // Install the exact 13-field historical payload in this disposable PostgreSQL fixture.
            Assert.Equal(1, await transaction.ExecuteAsync(
                "UPDATE public.event_log SET EventPayload=$1 WHERE EventVersion=$2;",
                [oldBytes, initial.EventVersion], token));
            return true;
        });
        await Send(save);
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
        var history = await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            transaction.QueryAsync(
                "SELECT EventVersion, EventPayload FROM public.event_log WHERE EventStreamId=$1 ORDER BY StreamVersion;",
                [stream], reader => EventLogMessagePackCodec.Shared.Deserialize<StrategyCatalogSourceEvent>(
                    reader.GetInt64(0), reader.GetFieldValue<byte[]>(1)), token));
        Assert.Equal(3, history.Count);
        var legacyCreation = history[0];
        Assert.Null(legacyCreation.AfterVersion);
        Assert.Equal(CatalogLifecycleStatus.Draft, legacyCreation.ReadAfterVersion().Status);
        var publish = history[1];
        Assert.Empty(publish.BeforeJson);
        Assert.Empty(publish.AfterJson);
        Assert.Equal(CatalogLifecycleStatus.Draft, publish.BeforeVersion?.Status);
        Assert.Equal(CatalogLifecycleStatus.Published, publish.AfterVersion?.Status);
        var fact = history[2];
        Assert.Equal(CatalogLifecycleStatus.Published, fact.BeforeVersion?.Status);
        Assert.Equal(CatalogLifecycleStatus.Retired, fact.AfterVersion?.Status);
        Assert.Equal(3, await infrastructure.ActorEventSourceDb.GetCommittedStreamVersionAsync(stream));
        var secondStream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(
            $"StrategyCatalog.{StrategyCatalogLifecycleCommand.Address(second.Key).Format()}");
        var secondEvent = Assert.Single(await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<StrategyCatalogSourceEvent>(secondStream));
        var secondFact = Assert.IsType<StrategyCatalogSourceEvent>(
            EventLogMessagePackCodec.Shared.Deserialize(secondEvent.EventTypeName, secondEvent.EventVersion, secondEvent.EventData));

        var legacy = new StrategyCatalogDefinition
        {
            Key = new(StrategyCatalogKind.ParameterSchema, Guid.NewGuid(), 1),
            Code = $"LegacyCompositionRulesSchema-{Guid.NewGuid():N}",
            Name = "Legacy composition rules schema",
            Settings = CompositionRulesSchema.Settings()
        };
        await context.InsertStrategyCatalogDraftAsync(legacy, 0, "legacy fixture");
        await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            StrategyCatalogReconstruction.CaptureBaselineAsync(context, transaction, token));
        var legacyStream = await infrastructure.ActorEventSourceDb.GetEventStreamIdAsync(
            $"StrategyCatalog.{StrategyCatalogLifecycleCommand.Address(legacy.Key).Format()}");
        var legacyEvent = Assert.Single(await infrastructure.ActorEventSourceDb.GetEventsFromSnapshotAsync<StrategyCatalogSourceEvent>(legacyStream));
        var legacyFact = Assert.IsType<StrategyCatalogSourceEvent>(
            EventLogMessagePackCodec.Shared.Deserialize(legacyEvent.EventTypeName, legacyEvent.EventVersion, legacyEvent.EventData));
        Assert.True(legacyFact.IsBaseline);
        var storedLegacy = Assert.IsType<StoredStrategyCatalogDefinition>(await context.GetStrategyCatalogAsync(legacy.Key));
        Assert.Empty(legacyFact.AfterJson);
        Assert.Null(legacyFact.BeforeVersion);
        var capturedLegacy = Assert.IsType<StoredStrategyCatalogDefinition>(legacyFact.AfterVersion);
        Assert.Equal(storedLegacy.ContentHash, capturedLegacy.ContentHash);
        Assert.Equal(storedLegacy.Definition.Settings.GetRawText(), capturedLegacy.Definition.Settings.GetRawText());
        Assert.Equal(0, await infrastructure.EventTransactions.ExecuteOnceAsync((transaction, token) =>
            StrategyCatalogReconstruction.CaptureBaselineAsync(context, transaction, token)));
        Assert.Equal(1, await infrastructure.ActorEventSourceDb.GetCommittedStreamVersionAsync(legacyStream));
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

        // Rebuild from mixed history. The legacy creation and typed successors share one stream.
        Assert.True(await rebuildService.ProjectAsync(legacyCreation, true, CancellationToken.None));
        var wrongPredecessor = publish with
        {
            BeforeVersion = publish.BeforeVersion! with { CreatedBy = "different principal" }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            rebuildService.ProjectAsync(wrongPredecessor, true, CancellationToken.None));
        Assert.Equal(CatalogLifecycleStatus.Draft, (await rebuilt.GetStrategyCatalogAsync(definition.Key))!.Status);
        Assert.True(await rebuildService.ProjectAsync(publish, true, CancellationToken.None));
        Assert.False(await rebuildService.ProjectAsync(publish, true, CancellationToken.None));
        Assert.True(await rebuildService.ProjectAsync(fact, true, CancellationToken.None));
        await rebuildTransactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await StrategyCatalogReconstruction.RebuildAsync(rebuilt, transaction, sourceHeads.Reverse().ToArray(), token);
            return true;
        });
        Assert.False(await rebuildService.ProjectAsync(fact, true, CancellationToken.None));
        Assert.Equal(StrategyCatalogJson.Write(fact.ReadAfterVersion()), StrategyCatalogJson.Write(await rebuilt.GetStrategyCatalogAsync(definition.Key)));
        Assert.Equal(StrategyCatalogJson.Write(secondFact.ReadAfterVersion()), StrategyCatalogJson.Write(await rebuilt.GetStrategyCatalogAsync(second.Key)));
        Assert.Equal(StrategyCatalogJson.Write(legacyFact.ReadAfterVersion()), StrategyCatalogJson.Write(await rebuilt.GetStrategyCatalogAsync(legacy.Key)));
        Assert.Equal(3, await infrastructure.ActorEventSourceDb.GetCommittedStreamVersionAsync(stream));
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

    private static byte[] LegacyEventPayload(StrategyCatalogSourceEvent source)
    {
        var reader = new MessagePackReader(EventLogMessagePackCodec.Shared.Serialize(source));
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        Assert.Equal(3, reader.ReadArrayHeader());
        writer.WriteArrayHeader(3);
        writer.WriteRaw(reader.ReadRaw());
        writer.WriteRaw(reader.ReadRaw());
        Assert.Equal(15, reader.ReadArrayHeader());
        writer.WriteArrayHeader(13);
        for (var key = 0; key < 13; key++)
            writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
