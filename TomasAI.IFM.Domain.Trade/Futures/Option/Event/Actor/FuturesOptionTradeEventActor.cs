using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
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

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [OptionTradeEndOfDayProcessedEvent.Verb] = message =>
                message.AsEvent<OptionTradeEndOfDayProcessedEvent>()!,
            [OptionTradeLegDataChangedEvent.Verb] = message =>
                message.AsEvent<OptionTradeLegDataChangedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IFuturesOptionTradeEventContext, IEvent, ILogger<FuturesOptionTradeEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IFuturesOptionTradeEventContext, IEvent, ILogger<FuturesOptionTradeEventActor>, ValueTask>>
        {
            [typeof(OptionTradeEndOfDayProcessedEvent)] = static (eventContext, domainEvent, logger) =>
                ((OptionTradeEndOfDayProcessedEvent)domainEvent).ExecuteAsync(eventContext, logger),
            [typeof(OptionTradeLegDataChangedEvent)] = static (eventContext, domainEvent, logger) =>
                ((OptionTradeLegDataChangedEvent)domainEvent).ExecuteAsync(eventContext, logger)
        }.ToFrozenDictionary();

    protected override IEvent ParseMessage(
        IEventActorContext<FuturesOptionTradeEventActor> context,
        IActorMessage message) =>
        ParseMappedEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesOptionTradeEventActor> context,
        IEvent domainEvent) =>
        ResolveMappedEventHandler(domainEvent, _receiveMap)(Typed(context), domainEvent, Typed(context).Logger);

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
    /// <summary>Gets the Portfolio-owned trade valuation boundary.</summary>
    IPortfolioTradeValuationApi PortfolioValuation { get; }

    /// <summary>Gets the status console writer.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionTradeEventActor> Logger { get; }
}

/// <summary>Provides the runtime context for the futures-option event actor.</summary>
public sealed class FuturesOptionTradeEventContext
    : EventActorContext,
        IEventActorContext<FuturesOptionTradeEventActor>,
        IFuturesOptionTradeEventContext
{
    /// <summary>Initializes the futures-option event actor context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="portfolioValuation">The Portfolio trade valuation boundary.</param>
    /// <param name="statusConsoleWriter">The status console writer.</param>
    /// <param name="logger">The actor logger.</param>
    public FuturesOptionTradeEventContext(
        IActorSupervisor supervisor,
        IPortfolioTradeValuationApi portfolioValuation,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesOptionTradeEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesOptionTradeEventActor.ActorName))
    {
        PortfolioValuation = portfolioValuation;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }

    /// <inheritdoc />
    public IPortfolioTradeValuationApi PortfolioValuation { get; }

    /// <inheritdoc />
    public IStatusConsoleWriter StatusConsoleWriter { get; }

    /// <inheritdoc />
    public ILogger<FuturesOptionTradeEventActor> Logger { get; }
}
