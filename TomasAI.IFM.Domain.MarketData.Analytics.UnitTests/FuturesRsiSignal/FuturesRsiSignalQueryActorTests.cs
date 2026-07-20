using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

public class FuturesRsiSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesRsiSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesRsiSignalQueryActor : FuturesRsiSignalQueryActor
    {
        public TestableFuturesRsiSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesRsiSignalQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query)
            => await OnLoadStateAsync(context, threadId, query);
    }

  
}
