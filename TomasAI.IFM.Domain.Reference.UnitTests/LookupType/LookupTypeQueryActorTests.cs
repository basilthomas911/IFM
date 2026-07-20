using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;


namespace TomasAI.IFM.Domain.Reference.UnitTests.LookupType;

public class LookupTypeQueryActorTests : IClassFixture<ReferenceTestFixture>
{
    readonly ReferenceTestFixture _fixture;

    public LookupTypeQueryActorTests(ReferenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableLookupTypeQueryActor : LookupTypeQueryActor
    {
        public TestableLookupTypeQueryActor(IDbContextFactory dbFactory, ILogger<LookupTypeQueryActor> logger)
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
