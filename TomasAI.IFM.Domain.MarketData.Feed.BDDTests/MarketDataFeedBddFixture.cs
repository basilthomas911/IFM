using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
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
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

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
        ILogger<FuturesBarDataCommandActor>? logger = null,
        IEventProjector<FuturesBarDataCommandActor>? eventProjector = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            eventProjector ?? Substitute.For<IEventProjector<FuturesBarDataCommandActor>>(),
            logger ?? Substitute.For<ILogger<FuturesBarDataCommandActor>>());

    public TestableFuturesBarDataQueryActor CreateQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesBarDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesBarDataQueryActor>>());

    public TestableFuturesClosingPriceCommandActor CreateClosingPriceCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesClosingPriceCommandActor>? logger = null,
        IEventProjector<FuturesClosingPriceCommandActor>? eventProjector = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            eventProjector ?? Substitute.For<IEventProjector<FuturesClosingPriceCommandActor>>(),
            logger ?? Substitute.For<ILogger<FuturesClosingPriceCommandActor>>());

    public TestableFuturesEodDataCommandActor CreateEodCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesEodDataCommandActor>? logger = null,
        IEventProjector<FuturesEodDataCommandActor>? eventProjector = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            eventProjector ?? Substitute.For<IEventProjector<FuturesEodDataCommandActor>>(),
            logger ?? Substitute.For<ILogger<FuturesEodDataCommandActor>>());

    public TestableFuturesEodDataQueryActor CreateEodQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesEodDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesEodDataQueryActor>>());

    public TestableFuturesOptionTickDataCommandActor CreateOptionTickCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesOptionTickDataCommandActor>? logger = null,
        IEventProjector<FuturesOptionTickDataCommandActor>? eventProjector = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            eventProjector ?? Substitute.For<IEventProjector<FuturesOptionTickDataCommandActor>>(),
            logger ?? Substitute.For<ILogger<FuturesOptionTickDataCommandActor>>());

    public TestableFuturesOptionTickDataQueryActor CreateOptionTickQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesOptionTickDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>());

    public TestableFuturesTickDataCommandActor CreateTickCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<FuturesTickDataCommandActor>? logger = null,
        IEventProjector<FuturesTickDataCommandActor>? eventProjector = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            eventProjector ?? Substitute.For<IEventProjector<FuturesTickDataCommandActor>>(),
            logger ?? Substitute.For<ILogger<FuturesTickDataCommandActor>>());

    public TestableFuturesTickDataQueryActor CreateTickQueryActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesTickDataQueryActor>? logger = null)
        => new(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesTickDataQueryActor>>());

    public TestableMarketDataFeedCommandActor CreateMarketDataFeedCommandActor(
        IEventSourceActorDbContext? dbEventSource = null,
        ILogger<MarketDataFeedCommandActor>? logger = null,
        IEventProjector<MarketDataFeedCommandActor>? eventProjector = null)
        => new(
            dbEventSource ?? Substitute.For<IEventSourceActorDbContext>(),
            eventProjector ?? Substitute.For<IEventProjector<MarketDataFeedCommandActor>>(),
            logger ?? Substitute.For<ILogger<MarketDataFeedCommandActor>>());

    public TestableMarketDataFeedQueryActor CreateMarketDataFeedQueryActor(
        ApplicationMarketDataApi? marketDataApi = null,
        ISequenceIdGenerator? sequenceIdGenerator = null,
        IDbContextFactory? dbFactory = null,
        ILogger<MarketDataFeedQueryActor>? logger = null)
        => new(
            marketDataApi ?? Substitute.For<ApplicationMarketDataApi>(),
            sequenceIdGenerator ?? Substitute.For<ISequenceIdGenerator>(),
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<MarketDataFeedQueryActor>>());
}

public sealed class TestableMarketDataFeedCommandActor(
    IEventSourceActorDbContext dbEventSource,
    IEventProjector<MarketDataFeedCommandActor> eventProjector,
    ILogger<MarketDataFeedCommandActor> logger)
    : MarketDataFeedCommandActor(TypedActorContextFactory.Command(dbEventSource, eventProjector, logger), eventProjector)
{
    public ValueTask InvokeOnStartup(ICommandActorContext<MarketDataFeedCommandActor> context) => OnStartup(context);
    public ICommand InvokeParseMessage(ICommandActorContext<MarketDataFeedCommandActor> context, NatsMsg<byte[]> message) => ParseMessage(context, message);
    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(ICommandActorContext<MarketDataFeedCommandActor> context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);
    public ValueTask InvokeOnValidateAsync(ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);
    public ValueTask<IActorState> InvokeOnLoadStateAsync(ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);
    public ValueTask InvokeOnSaveStateAsync(ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);
    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext<MarketDataFeedCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableMarketDataFeedQueryActor : MarketDataFeedQueryActor
{
    public IMarketDataFeedQueryContext Context { get; }

    public TestableMarketDataFeedQueryActor(
        ApplicationMarketDataApi marketDataApi,
        ISequenceIdGenerator sequenceIdGenerator,
        IDbContextFactory dbFactory,
        ILogger<MarketDataFeedQueryActor> logger)
        : this(TypedActorContextFactory.Query(marketDataApi, sequenceIdGenerator, dbFactory, logger)) { }

    TestableMarketDataFeedQueryActor(IMarketDataFeedQueryContext context)
        : base(context) => Context = context;

    public IQuery InvokeParseMessage(IQueryActorContext<MarketDataFeedQueryActor> context, NatsMsg<byte[]> message) => ParseMessage(context, message);
    public ValueTask InvokeReceiveAsync(IQueryActorContext<MarketDataFeedQueryActor> context, IQuery query) => ReceiveAsync(context, query);
    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext<MarketDataFeedQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}

public sealed class TestableFuturesBarDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    IEventProjector<FuturesBarDataCommandActor> eventProjector,
    ILogger<FuturesBarDataCommandActor> logger)
    : FuturesBarDataCommandActor(TypedActorContextFactory.Command(dbEventSource, eventProjector, logger), eventProjector)
{
    public ValueTask InvokeOnStartup(ICommandActorContext<FuturesBarDataCommandActor> context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext<FuturesBarDataCommandActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext<FuturesBarDataCommandActor> context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext<FuturesBarDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext<FuturesBarDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext<FuturesBarDataCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext<FuturesBarDataCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesBarDataQueryActor : FuturesBarDataQueryActor
{
    public IFuturesBarDataQueryContext Context { get; }

    public TestableFuturesBarDataQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesBarDataQueryActor> logger)
        : this(TypedActorContextFactory.Query(dbFactory, logger)) { }

    TestableFuturesBarDataQueryActor(IFuturesBarDataQueryContext context)
        : base(context) => Context = context;

    public IQuery InvokeParseMessage(IQueryActorContext<FuturesBarDataQueryActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext<FuturesBarDataQueryActor> context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext<FuturesBarDataQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}

public sealed class TestableFuturesClosingPriceCommandActor(
    IEventSourceActorDbContext dbEventSource,
    IEventProjector<FuturesClosingPriceCommandActor> eventProjector,
    ILogger<FuturesClosingPriceCommandActor> logger)
    : FuturesClosingPriceCommandActor(TypedActorContextFactory.Command(dbEventSource, eventProjector, logger), eventProjector)
{
    public ValueTask InvokeOnStartup(ICommandActorContext<FuturesClosingPriceCommandActor> context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext<FuturesClosingPriceCommandActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext<FuturesClosingPriceCommandActor> context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext<FuturesClosingPriceCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext<FuturesClosingPriceCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext<FuturesClosingPriceCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext<FuturesClosingPriceCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesEodDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    IEventProjector<FuturesEodDataCommandActor> eventProjector,
    ILogger<FuturesEodDataCommandActor> logger)
    : FuturesEodDataCommandActor(TypedActorContextFactory.Command(dbEventSource, eventProjector, logger), eventProjector)
{
    public ValueTask InvokeOnStartup(ICommandActorContext<FuturesEodDataCommandActor> context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext<FuturesEodDataCommandActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext<FuturesEodDataCommandActor> context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext<FuturesEodDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext<FuturesEodDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext<FuturesEodDataCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext<FuturesEodDataCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesEodDataQueryActor : FuturesEodDataQueryActor
{
    public IFuturesEodDataQueryContext Context { get; }

    public TestableFuturesEodDataQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesEodDataQueryActor> logger)
        : this(TypedActorContextFactory.Query(dbFactory, logger)) { }

    TestableFuturesEodDataQueryActor(IFuturesEodDataQueryContext context)
        : base(context) => Context = context;

    public IQuery InvokeParseMessage(IQueryActorContext<FuturesEodDataQueryActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext<FuturesEodDataQueryActor> context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext<FuturesEodDataQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}

public sealed class TestableFuturesOptionTickDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    IEventProjector<FuturesOptionTickDataCommandActor> eventProjector,
    ILogger<FuturesOptionTickDataCommandActor> logger)
    : FuturesOptionTickDataCommandActor(TypedActorContextFactory.Command(dbEventSource, eventProjector, logger), eventProjector)
{
    public ValueTask InvokeOnStartup(ICommandActorContext<FuturesOptionTickDataCommandActor> context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext<FuturesOptionTickDataCommandActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext<FuturesOptionTickDataCommandActor> context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext<FuturesOptionTickDataCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesOptionTickDataQueryActor : FuturesOptionTickDataQueryActor
{
    public IFuturesOptionTickDataQueryContext Context { get; }

    public TestableFuturesOptionTickDataQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesOptionTickDataQueryActor> logger)
        : this(TypedActorContextFactory.Query(dbFactory, logger)) { }

    TestableFuturesOptionTickDataQueryActor(IFuturesOptionTickDataQueryContext context)
        : base(context) => Context = context;

    public IQuery InvokeParseMessage(IQueryActorContext<FuturesOptionTickDataQueryActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext<FuturesOptionTickDataQueryActor> context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext<FuturesOptionTickDataQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}

public sealed class TestableFuturesTickDataCommandActor(
    IEventSourceActorDbContext dbEventSource,
    IEventProjector<FuturesTickDataCommandActor> eventProjector,
    ILogger<FuturesTickDataCommandActor> logger)
    : FuturesTickDataCommandActor(TypedActorContextFactory.Command(dbEventSource, eventProjector, logger), eventProjector)
{
    public ValueTask InvokeOnStartup(ICommandActorContext<FuturesTickDataCommandActor> context) => OnStartup(context);

    public ICommand InvokeParseMessage(ICommandActorContext<FuturesTickDataCommandActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask<ServiceResult<GuidResult>> InvokeReceiveAsync(
        ICommandActorContext<FuturesTickDataCommandActor> context, IActorState state, ICommand command)
        => ReceiveAsync(context, state, command);

    public ValueTask InvokeOnValidateAsync(
        ICommandActorContext<FuturesTickDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnValidateAsync(context, threadId, command);

    public ValueTask<IActorState> InvokeOnLoadStateAsync(
        ICommandActorContext<FuturesTickDataCommandActor> context, ActorThreadId threadId, ICommand command)
        => OnLoadStateAsync(context, threadId, command);

    public ValueTask InvokeOnSaveStateAsync(
        ICommandActorContext<FuturesTickDataCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => OnSaveStateAsync(context, threadId, state, command);

    public ValueTask<ServiceResult<GuidResult>> InvokeOnExceptionAsync(
        ICommandActorContext<FuturesTickDataCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception)
        => OnExceptionAsync(context, threadId, command, exception);
}

public sealed class TestableFuturesTickDataQueryActor : FuturesTickDataQueryActor
{
    public IFuturesTickDataQueryContext Context { get; }

    public TestableFuturesTickDataQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesTickDataQueryActor> logger)
        : this(TypedActorContextFactory.Query(dbFactory, logger)) { }

    TestableFuturesTickDataQueryActor(IFuturesTickDataQueryContext context)
        : base(context) => Context = context;

    public IQuery InvokeParseMessage(IQueryActorContext<FuturesTickDataQueryActor> context, NatsMsg<byte[]> message)
        => ParseMessage(context, message);

    public ValueTask InvokeReceiveAsync(IQueryActorContext<FuturesTickDataQueryActor> context, IQuery query)
        => ReceiveAsync(context, query);

    public ValueTask InvokeOnExceptionAsync(
        IQueryActorContext<FuturesTickDataQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
        => OnExceptionAsync(context, threadId, query, verb, exception);
}
