using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.Securities.UnitTests.FuturesOptionContract;

public class FuturesOptionContractQueryActorTests : IClassFixture<SecuritiesFixture>
{
    readonly SecuritiesFixture _fixture;

    public FuturesOptionContractQueryActorTests(SecuritiesFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFuturesOptionContractQueryActor : FuturesOptionContractQueryActor
    {
        public TestableFuturesOptionContractQueryActor(
            IDbContextFactory dbFactory,
            ILogger<FuturesOptionContractQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);


    }


}

