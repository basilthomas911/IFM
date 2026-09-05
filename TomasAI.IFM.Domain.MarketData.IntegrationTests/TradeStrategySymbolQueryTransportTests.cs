using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

/// <summary>Client/serialized actor/provider-boundary integration, without live feeds or storage.</summary>
public sealed class TradeStrategySymbolQueryTransportTests
{
    static TradeStrategySymbolReadModel Symbol() => new() { Id = 101, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures" };

    [Theory]
    [InlineData("success")]
    [InlineData("provider-failure")]
    [InlineData("missing-provider")]
    public async Task Nats_client_dispatches_serialized_family_query_to_market_data_provider(string mode)
    {
        var provider = Substitute.For<IMarketDataApi>(); var context = Context();
        context.MarketDataApi.Returns(mode == "missing-provider" ? null : provider);
        ServiceResult<TradeStrategySymbolReadModel[]> expected = mode == "success"
            ? new ServiceOk<TradeStrategySymbolReadModel[]>([Symbol()]) : new ServiceFailed<TradeStrategySymbolReadModel[]>(503, "provider unavailable");
        using var cancellation = new CancellationTokenSource();
        provider.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, cancellation.Token).Returns(expected);
        ServiceResult<TradeStrategySymbolReadModel[]>? reply = null;
        context.ReplyAsync(Arg.Any<ActorThreadId>(), GetTradeStrategySymbolsQuery.Verb, Arg.Any<ServiceResult<TradeStrategySymbolReadModel[]>>())
            .Returns(call => { reply = call.Arg<ServiceResult<TradeStrategySymbolReadModel[]>>(); return ValueTask.CompletedTask; });
        var actor = new Probe(context); var producer = Substitute.For<IActorProducer>();
        producer.RequestAsync<TradeStrategySymbolReadModel[], GetTradeStrategySymbolsQuery>(Arg.Any<ActorSubject>(), Arg.Any<GetTradeStrategySymbolsQuery>(), cancellation.Token)
            .Returns(call => new ValueTask<ServiceResult<TradeStrategySymbolReadModel[]>>(DispatchAsync(call.Arg<GetTradeStrategySymbolsQuery>())));

        async Task<ServiceResult<TradeStrategySymbolReadModel[]>> DispatchAsync(GetTradeStrategySymbolsQuery original)
        {
            original.Subject.ToString().Should().Contain("MarketDataQuery");
            var query = MessagePackSerializer.Deserialize<GetTradeStrategySymbolsQuery>(MessagePackSerializer.Serialize(original));
            query.Family.Should().Be(TradeStrategyFamilyType.Futures);
            var message = Substitute.For<IActorMessage>(); message.Subject.Returns(query.Subject);
            message.AsQuery<GetTradeStrategySymbolsQuery, TradeStrategySymbolReadModel[]>().Returns(query);
            await actor.Receive(context, actor.Parse(context, message), cancellation.Token);
            return reply!;
        }

        var client = new TomasAI.IFM.Application.Api.Nats.Client.MarketDataQueryApi(producer);
        var actual = await client.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, cancellation.Token);
        actual.Success.Should().Be(mode == "success");
        if (actual.Success) actual.Value.Should().Equal(Symbol());
        else { actual.ErrorCode.Should().Be(503); actual.ErrorMessage.Should().NotBeNullOrWhiteSpace(); }
        if (mode != "missing-provider") await provider.Received(1).GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, cancellation.Token);
    }

    [Fact]
    public async Task Cancellation_prevents_client_requests_and_actor_replies()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var producer = Substitute.For<IActorProducer>();
        var client = new TomasAI.IFM.Application.Api.Nats.Client.MarketDataQueryApi(producer);
        await FluentActions.Awaiting(() => client.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();
        producer.ReceivedCalls().Should().BeEmpty();
        var context = Context(); var actor = new Probe(context);
        var query = new GetTradeStrategySymbolsQuery { Family = TradeStrategyFamilyType.Futures };
        await FluentActions.Awaiting(() => actor.Receive(context, query, cancellation.Token).AsTask()).Should().ThrowAsync<OperationCanceledException>();
        await context.DidNotReceive().ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceResult<TradeStrategySymbolReadModel[]>>());
    }

    [Fact]
    public async Task Http_client_uses_market_data_route_and_family_parameter()
    {
        var transport = Substitute.For<IQueryServiceApi>();
        transport.ExecuteQueryAsync<TradeStrategySymbolReadModel[]>(MarketDataQueryUriPath.GetTradeStrategySymbols,
            Arg.Is<GetTradeStrategySymbolsParameter>(x => x.Family == TradeStrategyFamilyType.FuturesOption && x.QueryParams == "family=FuturesOption"), GetTradeStrategySymbolsQuery.ErrorId)
            .Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Symbol()]));
        var client = new TomasAI.IFM.Application.Api.Client.MarketDataQueryApi(transport);
        (await client.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption)).Value.Should().Equal(Symbol());
    }

    [Fact]
    public void Shared_family_enum_preserves_legacy_type_resolution_and_numeric_values()
    {
        typeof(TradeStrategyFamilyReadModel).Assembly.GetType(typeof(TradeStrategyFamilyType).FullName!).Should().Be(typeof(TradeStrategyFamilyType));
        Enum.GetValues<TradeStrategyFamilyType>().Select(x => (int)x).Should().Equal(0, 1, 2, 3, 4, 5, 6);
    }

    static IMarketDataQueryContext Context()
    {
        var context = Substitute.For<IMarketDataQueryContext>();
        context.Logger.Returns(Substitute.For<ILogger<MarketDataQueryActor>>());
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, MarketDataQueryActor.ActorName));
        return context;
    }
    sealed class Probe(IMarketDataQueryContext context) : MarketDataQueryActor(context)
    {
        public IQuery Parse(IMarketDataQueryContext ctx, IActorMessage message) => ParseMessage(ctx, message);
        public ValueTask Receive(IMarketDataQueryContext ctx, IQuery query, CancellationToken token) => ReceiveAsync(ctx, query, token);
    }
}
