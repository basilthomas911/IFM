using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.Exceptions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;

namespace TomasAI.IFM.Domain.Securities.UnitTests.FuturesOptionContract;

public class FuturesOptionContractCommandActorTests : IClassFixture<SecuritiesFixture>
{
    readonly SecuritiesFixture _fixture;

    public FuturesOptionContractCommandActorTests(SecuritiesFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFuturesOptionContractCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesOptionContractCommandActor> logger)
        : FuturesOptionContractCommandActor(dbEventSource, logger)
    {
        public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);
    }

}
