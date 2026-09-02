using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;

/// <summary>Statelessly routes current-contract trades to durable VWAP processing.</summary>
public sealed class FuturesVwapSignalRealtimeActor(
    IRealtimeActorContext<FuturesVwapSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesVwapSignalRealtimeActor>(actorContext,
        ((IFuturesVwapSignalRealtimeContext)actorContext).Logger)
{
    /// <summary>Identifies the VWAP Realtime mailbox.</summary>
    public const string ActorName = "FuturesVwapSignal";
    static readonly ActorTypeId Route = new(ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor, FuturesMarketPriceUpdatedRealtimeEvent.Verb);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!
        };
    IFuturesVwapSignalRealtimeContext TypedContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesVwapSignalRealtimeContext, nameof(actorContext))!;
    readonly FuturesVwapStreamOwnership streamOwnership = new();
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesVwapSignalRealtimeContext,
        FuturesContractV3ReadModel, ILogger, ValueTask<bool>>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesVwapSignalRealtimeContext,
            FuturesContractV3ReadModel, ILogger, ValueTask<bool>>>
    {
        [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = async (@event, context, contract, eventLogger) =>
            await ((FuturesMarketPriceUpdatedRealtimeEvent)@event)
                .ExecuteAsync(context, contract, eventLogger).ConfigureAwait(false)
    };

    /// <inheritdoc />
    protected override async ValueTask OnStartup(IEventActorContext<FuturesVwapSignalRealtimeActor> context)
    {
        context.AddRealtimeRouter(Route, Id);
        var configuration = FuturesVwapConfiguration.Standard;
        if (TypedContext.MarketDataApi.TryGetOnTheRunFuturesContract(
            configuration.RootSymbol, out _))
            _ = await streamOwnership.EnsureAsync(
                TypedContext.MarketDataApi, configuration.RootSymbol).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnShutdown(IEventActorContext<FuturesVwapSignalRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(Route, Id);
        await streamOwnership.ReleaseAsync(TypedContext.MarketDataApi).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesVwapSignalRealtimeActor> context,
        IActorMessage message) =>
        ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesVwapSignalRealtimeActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        FuturesContractV3ReadModel contract;
        try
        {
            contract = await streamOwnership.EnsureAsync(TypedContext.MarketDataApi,
                FuturesVwapConfiguration.Standard.RootSymbol).ConfigureAwait(false);
        }
        catch (MarketDataApiNotRunningException) { return; }
        _ = await handler(@event, TypedContext, contract, TypedContext.Logger).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesVwapSignalRealtimeActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
