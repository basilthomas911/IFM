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
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

public sealed class ConfigurationStrategyCatalogActorTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Serialized_catalog_commands_persist_publish_and_retire_exact_versions_in_postgres()
    {
        var settings = new DbConnectionSettings().Add(ConfigurationDbContext.ConfigurationDbConnection,
            Environment.GetEnvironmentVariable("IFM_POSTGRES_CONFIGURATION_TEST_CONNECTION") ?? "Host=localhost;Port=5432;Database=ifm-configuration-integration-tests", "System.Data.Postgres");
        var factory = Substitute.For<IDbContextFactory>(); var logger = Substitute.For<ILogger<DbProvider>>();
        var context = new ConfigurationDbContext(settings, factory, logger); factory.ConfigurationDb.Returns(context);
        await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
        var service = new StrategyCatalogService(factory);
        var actorContext = Substitute.For<ICommandActorContext<TradeStrategyFamilyCommandActor>>();
        actorContext.ActorId.Returns(new ActorMailboxId(ActorType.Command, StrategyCatalogCommand.Actor));
        var actor = new TradeStrategyFamilyCommandActor(actorContext,
            new TradeStrategyFamilyCreationService(Substitute.For<IMarketDataApi>(), Substitute.For<ITradeStrategyFamilyCatalogStore>(), TimeProvider.System),
            Substitute.For<ILogger<TradeStrategyFamilyCommandActor>>(), service);
        var definition = new StrategyCatalogDefinition { Key = new(StrategyCatalogKind.Family, Guid.NewGuid(), 1), Code = "ActorCatalog-" + Guid.NewGuid().ToString("N"), Name = "Catalog integration" };
        var save = new CatalogCommandRequest(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, definition);
        await Send(save); await Send(save);
        var row = StrategyCatalogJson.Read<StoredStrategyCatalogDefinition>(await service.QueryAsync(new(CatalogQueryOperation.Exact, Key: definition.Key)));
        row.Status.Should().Be(CatalogLifecycleStatus.Draft);
        var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        await Send(new(Guid.NewGuid(), CatalogCommandOperation.Publish, Key: definition.Key, ExpectedHash: row.ContentHash, EffectiveUtc: now));
        (await context.GetStrategyCatalogAsync(definition.Key))!.Status.Should().Be(CatalogLifecycleStatus.Published);
        var second = definition with { Key = definition.Key with { Version = 2 }, Description = "Editable revision" };
        await Send(new(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, second, ExpectedPreviousVersion: 1));
        await Send(new(Guid.NewGuid(), CatalogCommandOperation.Retire, Key: definition.Key, ExpectedHash: row.ContentHash, EffectiveUtc: now.AddSeconds(1)));
        (await context.GetStrategyCatalogAsync(definition.Key))!.Status.Should().Be(CatalogLifecycleStatus.Retired);
        (await context.GetStrategyCatalogAsync(second.Key))!.Status.Should().Be(CatalogLifecycleStatus.Draft);
        var listed = StrategyCatalogJson.Read<StrategyCatalogSummary[]>(await service.QueryAsync(new(CatalogQueryOperation.List, StrategyCatalogKind.Family, Limit: 128)));
        listed.Where(x => x.Key.Id == definition.Key.Id).Should().ContainSingle().Which.Key.Version.Should().Be(2);

        async Task Send(CatalogCommandRequest request)
        {
            var original = new StrategyCatalogCommand { CommandId = request.OperationId, RequestJson = StrategyCatalogJson.Write(request), Subject = new(ActorType.Command, StrategyCatalogCommand.Actor, StrategyCatalogCommand.Verb, "0") };
            var command = MessagePackSerializer.Deserialize<StrategyCatalogCommand>(MessagePackSerializer.Serialize(original));
            var message = Substitute.For<IActorMessage>(); message.Subject.Returns(command.Subject); message.AsCommand<StrategyCatalogCommand>().Returns(command);
            typeof(TradeStrategyFamilyCommandActor).GetMethod("ParseMessage", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.Invoke(actor, [actorContext, message]).Should().BeSameAs(command);
            var receive = typeof(TradeStrategyFamilyCommandActor).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(x => x.Name == "ReceiveAsync" && x.GetParameters().Length == 4);
            var result = await (ValueTask<ServiceResult<GuidResult>>)receive.Invoke(actor, [actorContext, null, command, CancellationToken.None])!;
            result.Success.Should().BeTrue(); result.Value!.Guid.Should().Be(request.OperationId);
        }
    }
}
