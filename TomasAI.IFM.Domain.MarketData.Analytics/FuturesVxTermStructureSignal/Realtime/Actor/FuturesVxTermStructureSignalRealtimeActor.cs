using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;

/// <summary>Statelessly routes front/back VX trade updates into durable Command processing.</summary>
public sealed class FuturesVxTermStructureSignalRealtimeActor(
    IRealtimeActorContext<FuturesVxTermStructureSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesVxTermStructureSignalRealtimeActor>(actorContext,
        ((IFuturesVxTermStructureSignalRealtimeContext)actorContext).Logger)
{
    /// <summary>Identifies the VX term-structure realtime mailbox.</summary>
    public const string ActorName = "FuturesVxTermStructureSignal";
    static readonly ActorTypeId Route = new(ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor, FuturesMarketPriceUpdatedRealtimeEvent.Verb);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!
        };
    /// <summary>Gets the typed realtime context supplied through open-generic registration.</summary>
    IFuturesVxTermStructureSignalRealtimeContext TypedContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesVxTermStructureSignalRealtimeContext, nameof(actorContext))!;
    readonly FuturesVxTermStructureStreamOwnership streamOwnership = new();
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesVxTermStructureSignalRealtimeContext,
        FuturesTermStructureContracts, ILogger, ValueTask<bool>>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesVxTermStructureSignalRealtimeContext,
            FuturesTermStructureContracts, ILogger, ValueTask<bool>>>
    {
        [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = async (@event, context, contracts, eventLogger) =>
            await ((FuturesMarketPriceUpdatedRealtimeEvent)@event)
                .ExecuteAsync(context, contracts, eventLogger).ConfigureAwait(false)
    };

    /// <inheritdoc />
    protected override async ValueTask OnStartup(
        IEventActorContext<FuturesVxTermStructureSignalRealtimeActor> context)
    {
        context.AddRealtimeRouter(Route, Id);
        if (TypedContext.MarketDataApi.TryGetFuturesTermStructureContracts("VX", out var contracts)
            && contracts.IsValid)
            _ = await streamOwnership.EnsureAsync(TypedContext.MarketDataApi).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async ValueTask OnShutdown(IEventActorContext<FuturesVxTermStructureSignalRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(Route, Id);
        await streamOwnership.ReleaseAsync(TypedContext.MarketDataApi).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesVxTermStructureSignalRealtimeActor> context,
        IActorMessage message) =>
        ParseMappedRealtimeEvent(context, message, _parseMap);
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesVxTermStructureSignalRealtimeActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        var contracts = await streamOwnership.EnsureAsync(TypedContext.MarketDataApi).ConfigureAwait(false);
        _ = await handler(@event, TypedContext, contracts, TypedContext.Logger).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesVxTermStructureSignalRealtimeActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
