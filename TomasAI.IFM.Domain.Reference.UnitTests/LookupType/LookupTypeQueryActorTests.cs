using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.LookupType.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.LookupType.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Queries;


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
            : this(CreateContext(dbFactory, logger))
        {
        }

        TestableLookupTypeQueryActor(ILookupTypeQueryContext context)
            : base(context)
        {
            LookupTypeContext = context;
        }

        static ILookupTypeQueryContext CreateContext(
            IDbContextFactory dbFactory,
            ILogger<LookupTypeQueryActor> logger)
        {
            var context = Substitute.For<ILookupTypeQueryContext>();
            context.ActorId.Returns(new ActorMailboxId(ActorType.Query, LookupTypeQueryActor.ActorName));
            context.DbFactory.Returns(dbFactory);
            context.Logger.Returns(logger);
            return context;
        }

        public ILookupTypeQueryContext LookupTypeContext { get; }

        public IQuery InvokeParseMessage(IQueryActorContext<LookupTypeQueryActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext<LookupTypeQueryActor> context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeReceiveAsync(
            IQueryActorContext<LookupTypeQueryActor> context,
            IQuery query,
            CancellationToken cancellationToken)
            => await ReceiveAsync(context, query, cancellationToken);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext<LookupTypeQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);


    }

    [Fact]
    public async Task ReceiveAsync_WithCancellation_PropagatesTokenAndDoesNotReply()
    {
        var referenceDb = Substitute.For<IReferenceDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.ReferenceDb.Returns(referenceDb);
        var actor = _fixture.CreateActor(
            dbFactory,
            Substitute.For<ILogger<LookupTypeQueryActor>>());
        var query = new GetLookupTypesQuery
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetLookupTypesQuery.Actor,
                GetLookupTypesQuery.Verb,
                "all")
        };
        var context = actor.LookupTypeContext;
        using var cancellation = new CancellationTokenSource();
        referenceDb.GetLookupTypesAsync(cancellation.Token)
            .Returns(_ => Task.FromCanceled<ICollection<LookupTypeReadModel>>(cancellation.Token));
        cancellation.Cancel();

        Func<Task> act = () => actor
            .InvokeReceiveAsync(context, query, cancellation.Token)
            .AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        await referenceDb.Received(1).GetLookupTypesAsync(cancellation.Token);
        context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<LookupTypeCollection>)!);
    }

}
