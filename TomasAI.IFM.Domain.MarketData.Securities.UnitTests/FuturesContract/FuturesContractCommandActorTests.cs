using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.UnitTests;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests.FuturesContract;

public class FuturesContractCommandActorTests : IClassFixture<SecuritiesFixture>
{
    readonly SecuritiesFixture _fixture;

    public FuturesContractCommandActorTests(SecuritiesFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFuturesContractCommandActor(IEventSourceActorDbContext dbEventSource, ILogger<FuturesContractCommandActor> logger)
        : FuturesContractCommandActor(CreateContext(dbEventSource, logger), Substitute.For<IEventProjector<FuturesContractCommandActor>>())
    {
        static ICommandActorContext<FuturesContractCommandActor> CreateContext(
            IEventSourceActorDbContext dbEventSource, ILogger<FuturesContractCommandActor> logger)
        {
            var context = Substitute.For<IFuturesContractCommandContext>();
            context.ActorId.Returns(new ActorMailboxId(ActorType.Command, FuturesContractCommandActor.ActorName));
            context.Logger.Returns(logger);
            context.DbEventSource.Returns(dbEventSource);
            context.ReferenceLookupService.Returns(Substitute.For<IReferenceLookupService>());
            return context;
        }
        public ICommand InvokeParseMessage(ICommandActorContext<FuturesContractCommandActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext<FuturesContractCommandActor> context, IActorState state, ICommand cmd)
            => await ReceiveAsync(context, state, cmd);

        public async ValueTask InvokeOnValidateAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, ICommand cmd)
            => await OnValidateAsync(context, threadId, cmd);

        public async ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(ICommandActorContext<FuturesContractCommandActor> context, ActorThreadId threadId, ICommand cmd, Exception ex)
            => await OnExceptionAsync(context, threadId, cmd, ex);
    }

  
}

