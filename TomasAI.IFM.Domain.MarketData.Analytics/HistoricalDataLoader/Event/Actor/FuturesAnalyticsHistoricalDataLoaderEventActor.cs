using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;

/// <summary>Runs one durable external history acquisition after its Requested event is delivered.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderEventActor(
    IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> actorContext)
    : BaseEventActor<FuturesAnalyticsHistoricalDataLoaderEventActor>(actorContext, actorContext.Logger)
{
    readonly IFuturesAnalyticsHistoricalDataLoaderEventContext _domainContext =
        (IFuturesAnalyticsHistoricalDataLoaderEventContext)actorContext;
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string ActorName = FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Verb] = static message =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderRequestedEvent>()!,
            [FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Verb] = static message =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderCompletedEvent>()!,
            [FuturesAnalyticsHistoricalDataLoaderFailedEvent.Verb] = static message =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderFailedEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent,
        IFuturesAnalyticsHistoricalDataLoaderEventContext,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent,
            IFuturesAnalyticsHistoricalDataLoaderEventContext,
            ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor>, ValueTask>>
        {
            [typeof(FuturesAnalyticsHistoricalDataLoaderRequestedEvent)] = static (@event, context, logger) =>
                ((FuturesAnalyticsHistoricalDataLoaderRequestedEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesAnalyticsHistoricalDataLoaderCompletedEvent)] = static (@event, context, logger) =>
                ((FuturesAnalyticsHistoricalDataLoaderCompletedEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesAnalyticsHistoricalDataLoaderFailedEvent)] = static (@event, context, logger) =>
                ((FuturesAnalyticsHistoricalDataLoaderFailedEvent)@event).ExecuteAsync(context, logger)
        };

    /// <inheritdoc />
    protected override ValueTask OnStartup(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, _domainContext, _domainContext.Logger);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
