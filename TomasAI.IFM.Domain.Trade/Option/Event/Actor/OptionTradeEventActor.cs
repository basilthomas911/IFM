using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Option.Event.Extensions;

namespace TomasAI.IFM.Domain.Trade.Option.Event.Actor;

/// <summary>Provides the OptionTradeEventActor implementation.</summary>
public class OptionTradeEventActor(
    IEventActorContext<OptionTradeEventActor> actorContext)
    : BaseEventActor<OptionTradeEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IOptionTradeEventContext ActorContext =>
        IsArgumentNull.Set(Context as IOptionTradeEventContext, nameof(Context))!;

    public const string Actor = "OptionTradeEvent";
    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<OptionTradeEventActor>, IEventActorContext, IStatusConsoleWriter, ILogger, ValueTask<bool>>> _receiveMap = new Dictionary<Type, Func<IEvent, IEventActorContext<OptionTradeEventActor>, IEventActorContext, IStatusConsoleWriter, ILogger, ValueTask<bool>>>()
    {
        [typeof(OptionTradeEndOfDayProcessedEvent)] = static (evt, ctx, _, _, _)
            => ((OptionTradeEndOfDayProcessedEvent)evt).ProcessFundEndOfDayAsync(ctx),
        [typeof(OptionTradeLegDataChangedEvent)] = static (evt, ctx, commandApi, statusConsoleWriter, logger)
            => ((OptionTradeLegDataChangedEvent)evt).ExecuteAsync(ctx, commandApi, statusConsoleWriter, logger)
    };

    protected override ValueTask OnStartup(IEventActorContext<OptionTradeEventActor> context)
    {
        _ = context;
        return ValueTask.CompletedTask;
    }

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
        [OptionTradeEndOfDayProcessedEvent.Verb] = msg => msg.AsEvent<OptionTradeEndOfDayProcessedEvent>()!,
        [OptionTradeLegDataChangedEvent.Verb] = msg => msg.AsEvent<OptionTradeLegDataChangedEvent>()!,
    };

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb, or <see langword="null"/> if the message subject
    /// does not correspond to a known event (indicating the message should be ignored).</returns>
    protected override IEvent ParseMessage(IEventActorContext<OptionTradeEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <summary>
    /// Receives an event and dispatches it to the appropriate handler based on the event's type.
    /// If no handler is found for the event type, an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    /// <param name="context">The event actor context in which the event is being processed.</param>
    /// <param name="event">The event to be processed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override ValueTask ReceiveAsync(IEventActorContext<OptionTradeEventActor> context, IEvent @event)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var receiveFunc = ResolveMappedEventHandler(@event, _receiveMap);
        return AwaitHandlerAsync(receiveFunc.Invoke(
            @event,
            dispatchContext,
            dispatchContext,
            ActorContext.StatusConsoleWriter,
            ActorContext.Logger));

        static async ValueTask AwaitHandlerAsync(ValueTask<bool> operation)
            => _ = await operation.ConfigureAwait(false);
    }

    /// <summary>
    /// Handles exceptions that occur during event processing by sending an error event to the event service.
    /// </summary>
    /// <param name="context">The event actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was raised.</param>
    /// <param name="event">The event being processed when the exception was thrown.</param>
    /// <param name="ex">The exception that was thrown during actor processing.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<OptionTradeEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(@event);
            await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
        }
        catch (Exception innerEx)
        {
            await innerEx.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
            Context.Logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
