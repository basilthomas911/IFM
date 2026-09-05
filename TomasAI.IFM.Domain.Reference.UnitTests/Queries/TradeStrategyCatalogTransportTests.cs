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

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class TradeStrategyCatalogTransportTests
{
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
    public async Task Serialized_creation_command_reaches_service_and_returns_same_operation_acknowledgement()
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
        var result = await (ValueTask<ServiceResult<GuidResult>>)receive.Invoke(actor, [ctx, null, command, CancellationToken.None])!;
        result.Success.Should().BeTrue();
        await store.Received(1).CreateAsync(request, Arg.Is<TradeStrategyFamilyReadModel>(x => x.Exchange == "XCME" && x.TradeStrategySymbolId == 10), Arg.Any<CancellationToken>());
    }

    sealed class Probe(IReferenceQueryContext ctx) : ReferenceQueryActor(ctx)
    {
        public IQuery Parse(IReferenceQueryContext context, IActorMessage message) => ParseMessage(context, message);
        public ValueTask Receive(IReferenceQueryContext context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    }
}
