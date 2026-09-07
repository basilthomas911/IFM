using System.Reflection;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.TradeStrategyFamilies;
using TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class TradeStrategyCatalogTransportTests
{
    [Fact]
    public async Task Serialized_lookup_query_reads_configurationdb_and_replies_with_group_rows()
    {
        var ctx = Substitute.For<IReferenceQueryContext>();
        ctx.Logger.Returns(Substitute.For<ILogger<ReferenceQueryActor>>());
        ctx.ActorId.Returns(new ActorMailboxId(ActorType.Query, ReferenceQueryActor.ActorName));
        var row = new LookupDefinitionReadModel(1, LookupDefinitionGroups.AssetTypes, "Futures", "Futures", "", 10, true, DateTime.UtcNow, DateTime.UtcNow);
        ctx.DbFactory.ConfigurationDb.GetLookupDefinitionsAsync(LookupDefinitionGroups.AssetTypes, Arg.Any<CancellationToken>()).Returns([row]);
        var query = MessagePackSerializer.Deserialize<GetLookupDefinitionsQuery>(MessagePackSerializer.Serialize(new GetLookupDefinitionsQuery
        {
            GroupName = LookupDefinitionGroups.AssetTypes,
            Subject = new ActorSubject(ActorType.Query, GetLookupDefinitionsQuery.Actor, GetLookupDefinitionsQuery.Verb, "0")
        }));
        var message = Substitute.For<IActorMessage>(); message.Subject.Returns(query.Subject);
        message.AsQuery<GetLookupDefinitionsQuery, LookupDefinitionReadModel[]>().Returns(query);
        var actor = new Probe(ctx); actor.Parse(ctx, message).Should().BeSameAs(query);
        await actor.Receive(ctx, query);
        await ctx.Received(1).ReplyAsync(query.Subject.ThreadId, GetLookupDefinitionsQuery.Verb,
            Arg.Is<ServiceResult<LookupDefinitionReadModel[]>>(x => x.Success && x.Value!.Length == 1 && x.Value[0] == row));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Serialized_symbol_query_dispatches_and_replies_with_provider_result(bool available)
    {
        var api = Substitute.For<IMarketDataApi>();
        var ctx = Substitute.For<IReferenceQueryContext>();
        ctx.Logger.Returns(Substitute.For<ILogger<ReferenceQueryActor>>());
        ctx.ActorId.Returns(new ActorMailboxId(ActorType.Query, ReferenceQueryActor.ActorName));
        ctx.MarketDataApi.Returns(available ? api : null);
        var expected = new ServiceOk<TradeStrategySymbolReadModel[]>([new() { Id = 10, Symbol = "ES", Exchange = "XCME", Currency = "USD", Description = "ES futures" }]);
        api.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, Arg.Any<CancellationToken>()).Returns(expected);
        var query = MessagePackSerializer.Deserialize<GetTradeStrategySymbolsQuery>(MessagePackSerializer.Serialize(new GetTradeStrategySymbolsQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetTradeStrategySymbolsQuery.Actor, GetTradeStrategySymbolsQuery.Verb, "0"), Family = TradeStrategyFamilyType.Futures
        }));
        var message = Substitute.For<IActorMessage>(); message.Subject.Returns(query.Subject);
        message.AsQuery<GetTradeStrategySymbolsQuery, TradeStrategySymbolReadModel[]>().Returns(query);
        var actor = new Probe(ctx);
        actor.Parse(ctx, message).Should().BeSameAs(query);
        await actor.Receive(ctx, query);
        await ctx.Received(1).ReplyAsync(query.Subject.ThreadId, GetTradeStrategySymbolsQuery.Verb,
            Arg.Is<ServiceResult<TradeStrategySymbolReadModel[]>>(x => x.Success == available && (!available || x.Value![0].Id == 10)));
    }

    [Fact]
    public async Task Legacy_creation_command_remains_deserializable_but_rejects_active_writes()
    {
        var request = new CreateTradeStrategyFamilyRequest { OperationId = Guid.NewGuid(), Family = TradeStrategyFamilyType.Futures, Strategy = TradeStrategyType.Futures, TimeFrame = TimeFrameType.Daily, TradeStrategySymbolId = 10, Description = "Daily ES" };
        var command = MessagePackSerializer.Deserialize<CreateTradeStrategyFamilyCommand>(MessagePackSerializer.Serialize(new CreateTradeStrategyFamilyCommand
        {
            CommandId = request.OperationId, Request = request,
            Subject = new ActorSubject(ActorType.Command, CreateTradeStrategyFamilyCommand.Actor, CreateTradeStrategyFamilyCommand.Verb, "0")
        }));
        command.Request.Should().Be(request);
        command.RouteTo.Should().Be(BoundedContextName.TradeStrategyFamilyBoundedContext);
        var ctx = Substitute.For<ICommandActorContext<TradeStrategyFamilyCommandActor>>();
        ctx.ActorId.Returns(new ActorMailboxId(ActorType.Command, CreateTradeStrategyFamilyCommand.Actor));
        var api = Substitute.For<IMarketDataApi>(); var store = Substitute.For<ITradeStrategyFamilyCatalogStore>();
        api.GetTradeStrategySymbolsAsync(request.Family, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([new() { Id = 10, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures" }]));
        store.CreateAsync(request, Arg.Any<TradeStrategyFamilyReadModel>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<TradeStrategyFamilyReadModel>() with { TradeStrategyFamilyId = 20, DefinitionVersion = 1 });
        var actor = new TradeStrategyFamilyCommandActor(ctx, new TradeStrategyFamilyCreationService(api, store, TimeProvider.System), Substitute.For<ILogger<TradeStrategyFamilyCommandActor>>());
        var receive = typeof(TradeStrategyFamilyCommandActor).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(x => x.Name == "ReceiveAsync" && x.DeclaringType == typeof(TradeStrategyFamilyCommandActor) && x.GetParameters().Length == 4);
        await FluentActions.Invoking(async () => await (ValueTask<ServiceResult<GuidResult>>)receive.Invoke(actor, [ctx, null, command, CancellationToken.None])!)
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Legacy*read-only*");
        store.ReceivedCalls().Should().BeEmpty();

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_change_and_remove_remain_deserializable_but_cannot_write(bool remove)
    {
        var operation = Guid.NewGuid(); var target = new TradeStrategyFamilyReference(20, 3);
        var definition = new CreateTradeStrategyFamilyRequest { OperationId = operation, Family = TradeStrategyFamilyType.Futures,
            Strategy = TradeStrategyType.Futures, TimeFrame = TimeFrameType.Weekly, TradeStrategySymbolId = 10, Description = "Changed weekly ES" };
        var changeRequest = new ChangeTradeStrategyFamilyRequest { OperationId = operation, Target = target, Definition = definition };
        var removeRequest = new RemoveTradeStrategyFamilyRequest { OperationId = operation, Target = target };
        var message = Substitute.For<IActorMessage>();
        ICommand command;
        if (remove)
        {
            var typed = MessagePackSerializer.Deserialize<RemoveTradeStrategyFamilyCommand>(MessagePackSerializer.Serialize(new RemoveTradeStrategyFamilyCommand
            {
                CommandId = operation, Request = removeRequest,
                Subject = new ActorSubject(ActorType.Command, RemoveTradeStrategyFamilyCommand.Actor, RemoveTradeStrategyFamilyCommand.Verb, "0")
            }));
            typed.Request.Should().Be(removeRequest); command = typed;
            message.Subject.Returns(typed.Subject); message.AsCommand<RemoveTradeStrategyFamilyCommand>().Returns(typed);
        }
        else
        {
            var typed = MessagePackSerializer.Deserialize<ChangeTradeStrategyFamilyCommand>(MessagePackSerializer.Serialize(new ChangeTradeStrategyFamilyCommand
            {
                CommandId = operation, Request = changeRequest,
                Subject = new ActorSubject(ActorType.Command, ChangeTradeStrategyFamilyCommand.Actor, ChangeTradeStrategyFamilyCommand.Verb, "0")
            }));
            typed.Request.Should().Be(changeRequest); command = typed;
            message.Subject.Returns(typed.Subject); message.AsCommand<ChangeTradeStrategyFamilyCommand>().Returns(typed);
        }
        var ctx = Substitute.For<ICommandActorContext<TradeStrategyFamilyCommandActor>>();
        ctx.ActorId.Returns(new ActorMailboxId(ActorType.Command, CreateTradeStrategyFamilyCommand.Actor));
        var api = Substitute.For<IMarketDataApi>(); var store = Substitute.For<ITradeStrategyFamilyCatalogStore>();
        api.GetTradeStrategySymbolsAsync(definition.Family, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>(
            [new() { Id = 10, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures" }]));
        var actor = new TradeStrategyFamilyCommandActor(ctx, new TradeStrategyFamilyCreationService(api, store, TimeProvider.System), Substitute.For<ILogger<TradeStrategyFamilyCommandActor>>());
        var parse = typeof(TradeStrategyFamilyCommandActor).GetMethod("ParseMessage", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        parse.Invoke(actor, [ctx, message]).Should().BeSameAs(command);
        var receive = typeof(TradeStrategyFamilyCommandActor).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(x => x.Name == "ReceiveAsync" && x.DeclaringType == typeof(TradeStrategyFamilyCommandActor) && x.GetParameters().Length == 4);
        await FluentActions.Invoking(async () => await (ValueTask<ServiceResult<GuidResult>>)receive.Invoke(actor, [ctx, null, command, CancellationToken.None])!)
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Legacy*read-only*");
        store.ReceivedCalls().Should().BeEmpty();

    }

    sealed class Probe(IReferenceQueryContext ctx) : ReferenceQueryActor(ctx)
    {
        public IQuery Parse(IReferenceQueryContext context, IActorMessage message) => ParseMessage(context, message);
        public ValueTask Receive(IReferenceQueryContext context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    }
}
