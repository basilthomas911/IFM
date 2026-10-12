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
public class FuturesOptionTradeEventActor(IEventActorContext<FuturesOptionTradeEventActor> context) : BaseEventActor<FuturesOptionTradeEventActor>(context, Typed(context).Logger)
{
    public const string ActorName = FuturesOptionTradeActorNames.Event;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [OptionTradeEndOfDayProcessedEvent.Verb] = message => message.AsEvent<OptionTradeEndOfDayProcessedEvent>()!,
        [OptionTradeLegDataChangedEvent.Verb] = message => message.AsEvent<OptionTradeLegDataChangedEvent>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IFuturesOptionTradeEventContext, IEvent, ILogger<FuturesOptionTradeEventActor>, ValueTask>> _receiveMap = new Dictionary<Type, Func<IFuturesOptionTradeEventContext, IEvent, ILogger<FuturesOptionTradeEventActor>, ValueTask>>
    {
        [typeof(OptionTradeEndOfDayProcessedEvent)] = static (eventContext, domainEvent, logger) => ((OptionTradeEndOfDayProcessedEvent)domainEvent).ExecuteAsync(eventContext, logger),
        [typeof(OptionTradeLegDataChangedEvent)] = static (eventContext, domainEvent, logger) => ((OptionTradeLegDataChangedEvent)domainEvent).ExecuteAsync(eventContext, logger)
    }.ToFrozenDictionary();
    protected override IEvent ParseMessage(IEventActorContext<FuturesOptionTradeEventActor> context, IActorMessage message) => ParseMappedEvent(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IEventActorContext<FuturesOptionTradeEventActor> context, IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)(Typed(context), @event, Typed(context).Logger);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This is the actor exception boundary; a failed error-event publication is logged locally without recursively publishing or escaping.")]
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesOptionTradeEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        try
        {
            await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
        }
        catch (Exception reportingException)
        {
            // A failed error publication is logged locally rather than recursively published.
            TradeBoundaryDiagnosticLog.EventExceptionReportFailed(Typed(context).Logger, reportingException, nameof(FuturesOptionTradeEventActor), nameof(OnExceptionAsync), ActorName);
        }
    }

    static IFuturesOptionTradeEventContext Typed(IEventActorContext<FuturesOptionTradeEventActor> context) => context as IFuturesOptionTradeEventContext ?? throw new ArgumentException("Typed Futures Option Trade event context required.", nameof(context));
}
