using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.Securities.UnitTests.FuturesContract;

public class FuturesContractQueryActorTests : IClassFixture<SecuritiesFixture>
{
    readonly SecuritiesFixture _fixture;

    public FuturesContractQueryActorTests(SecuritiesFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFuturesContractQueryActor : FuturesContractQueryActor
    {
        public TestableFuturesContractQueryActor(IDbContextFactory dbFactory, ILogger<FuturesContractQueryActor> logger)
            : base(dbFactory,logger)
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


