using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests;

public sealed class MarketDataFeedBddFixture
{
    public MarketDataFeedBddFixture()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    public TestableFuturesBarDataCommandActor CreateCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesBarDataCommandActor>? logger = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            logger ?? Substitute.For<ILogger<FuturesBarDataCommandActor>>());

    public TestableFuturesBarDataQueryActor CreateQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesBarDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesBarDataQueryActor>>());

    public TestableFuturesClosingPriceCommandActor CreateClosingPriceCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesClosingPriceCommandActor>? logger = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            logger ?? Substitute.For<ILogger<FuturesClosingPriceCommandActor>>());

    public TestableFuturesEodDataCommandActor CreateEodCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesEodDataCommandActor>? logger = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            logger ?? Substitute.For<ILogger<FuturesEodDataCommandActor>>());

    public TestableFuturesEodDataQueryActor CreateEodQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesEodDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesEodDataQueryActor>>());

    public TestableFuturesOptionTickDataCommandActor CreateOptionTickCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesOptionTickDataCommandActor>? logger = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            logger ?? Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>());

    public TestableFuturesOptionTickDataQueryActor CreateOptionTickQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesOptionTickDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>());

    public TestableFuturesOptionQuoteDataCommandActor CreateOptionQuoteCommandActor(
        IReferenceLookupService? referenceLookupService = null,
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesOptionQuoteDataCommandActor>? logger = null)
        => new(
            referenceLookupService ?? Substitute.For<IReferenceLookupService>(),
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            logger ?? Substitute.For<ILogger<FuturesOptionQuoteDataCommandActor>>());

    public TestableFuturesTickDataCommandActor CreateTickCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesTickDataCommandActor>? logger = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            logger ?? Substitute.For<ILogger<FuturesTickDataCommandActor>>());

    public TestableFuturesTickDataQueryActor CreateTickQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesTickDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesTickDataQueryActor>>());
}

public sealed class TestableFuturesBarDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<FuturesBarDataCommandActor> logger)
    : FuturesBarDataCommandActor(dbEventSource, logger)
{
    public ValueTask InvokeOnStartup(ICommandActorContext context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesBarDataQueryActor(
    IDbContextFactory dbFactory,
    ILogger<FuturesBarDataQueryActor> logger)
    : FuturesBarDataQueryActor(dbFactory, logger)
{
    public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}

public sealed class TestableFuturesClosingPriceCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<FuturesClosingPriceCommandActor> logger)
    : FuturesClosingPriceCommandActor(dbEventSource, logger)
{
    public ValueTask InvokeOnStartup(ICommandActorContext context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesEodDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<FuturesEodDataCommandActor> logger)
    : FuturesEodDataCommandActor(dbEventSource, logger)
{
    public ValueTask InvokeOnStartup(ICommandActorContext context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesEodDataQueryActor(
    IDbContextFactory dbFactory,
    ILogger<FuturesEodDataQueryActor> logger)
    : FuturesEodDataQueryActor(dbFactory, logger)
{
    public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}

public sealed class TestableFuturesOptionTickDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<FuturesOptionTickDataCommandActor> logger)
    : FuturesOptionTickDataCommandActor(dbEventSource, logger)
{
    public ValueTask InvokeOnStartup(ICommandActorContext context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesOptionTickDataQueryActor(
    IDbContextFactory dbFactory,
    ILogger<FuturesOptionTickDataQueryActor> logger)
    : FuturesOptionTickDataQueryActor(dbFactory, logger)
{
    public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}

public sealed class TestableFuturesOptionQuoteDataCommandActor(
    IReferenceLookupService referenceLookupService,
    IEventSourceActorDbContext dbEventSource,
    ILogger<FuturesOptionQuoteDataCommandActor> logger)
    : FuturesOptionQuoteDataCommandActor(referenceLookupService, dbEventSource, logger)
{
    public ValueTask InvokeOnStartup(ICommandActorContext context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesTickDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    ILogger<FuturesTickDataCommandActor> logger)
    : FuturesTickDataCommandActor(dbEventSource, logger)
{
    public ValueTask InvokeOnStartup(ICommandActorContext context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesTickDataQueryActor(
    IDbContextFactory dbFactory,
    ILogger<FuturesTickDataQueryActor> logger)
    : FuturesTickDataQueryActor(dbFactory, logger)
{
    public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}
