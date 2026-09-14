using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;

/// <summary>Processes cross-domain events emitted by established futures-option trades.</summary>
public class FuturesOptionTradeEventActor(
    IEventActorContext<FuturesOptionTradeEventActor> context)
    : BaseEventActor<FuturesOptionTradeEventActor>(context, Typed(context).Logger)
{
    public const string ActorName = FuturesOptionTradeActorNames.Event;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [OptionTradeEndOfDayProcessedEvent.Verb] = message =>
                message.AsEvent<OptionTradeEndOfDayProcessedEvent>()!,
            [OptionTradeLegDataChangedEvent.Verb] = message =>
                message.AsEvent<OptionTradeLegDataChangedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IFuturesOptionTradeEventContext, IEvent, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<IFuturesOptionTradeEventContext, IEvent, ValueTask>>
        {
            [typeof(OptionTradeEndOfDayProcessedEvent)] = static (eventContext, domainEvent) =>
                ((OptionTradeEndOfDayProcessedEvent)domainEvent).ExecuteAsync(eventContext),
            [typeof(OptionTradeLegDataChangedEvent)] = static (eventContext, domainEvent) =>
                ((OptionTradeLegDataChangedEvent)domainEvent).ExecuteAsync(eventContext)
        }.ToFrozenDictionary();

    protected override IEvent ParseMessage(
        IEventActorContext<FuturesOptionTradeEventActor> context,
        IActorMessage message) =>
        ParseMappedEvent(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesOptionTradeEventActor> context,
        IEvent domainEvent) =>
        ResolveMappedEventHandler(domainEvent, ReceiveMap)(Typed(context), domainEvent);

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesOptionTradeEventActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception)
    {
        try
        {
            await exception.SendErrorEventAsync<
                global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
                ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
        }
        catch (Exception reportingException)
        {
            await reportingException.SendErrorEventAsync<
                global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
                ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
            Typed(context).Logger.LogError(
                reportingException,
                "Failed to report an event exception for {ActorName}.",
                ActorName);
        }
    }

    static IFuturesOptionTradeEventContext Typed(
        IEventActorContext<FuturesOptionTradeEventActor> context) =>
        context as IFuturesOptionTradeEventContext
        ?? throw new ArgumentException("Typed Futures Option Trade event context required.");
}

/// <summary>Defines runtime services available to futures-option event handlers.</summary>
public interface IFuturesOptionTradeEventContext :
    IEventActorContext<FuturesOptionTradeEventActor>
{
    IStatusConsoleWriter StatusConsoleWriter { get; }
    ILogger<FuturesOptionTradeEventActor> Logger { get; }
}

/// <summary>Provides the runtime context for the futures-option event actor.</summary>
public sealed class FuturesOptionTradeEventContext(
    IActorSupervisor supervisor,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesOptionTradeEventActor> logger)
    : EventActorContext(
        supervisor,
        new ActorMailboxId(ActorType.Event, FuturesOptionTradeEventActor.ActorName)),
        IEventActorContext<FuturesOptionTradeEventActor>,
        IFuturesOptionTradeEventContext
{
    public IStatusConsoleWriter StatusConsoleWriter { get; } = statusConsoleWriter;
    public ILogger<FuturesOptionTradeEventActor> Logger { get; } = logger;
}
