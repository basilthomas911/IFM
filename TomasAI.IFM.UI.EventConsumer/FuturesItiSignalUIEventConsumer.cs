using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes and processes UI events related to futures ITI signals.
/// </summary>
/// <remarks>This class subscribes to specific market data analytic events and executes provided actions when
/// these events occur. It handles events such as trend direction changes, trend extreme changes, and futures trade
/// signal updates.</remarks>
/// <param name="options"></param>
/// <param name="logger"></param>
public class FuturesItiSignalUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IFuturesItiSignalUIEventConsumer
{
    readonly ILogger _logger = logger;
    readonly static string EventConsumer = "FuturesEodDataUIEventConsumer";
    readonly Dictionary<ActorMailboxId, List<string>> _eventMap = new()
    {
        [new ActorMailboxId(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor)]
               = [FuturesItiSignalGeneratedEvent.Verb],
        [new ActorMailboxId(ActorType.Event, FuturesTradeSignalUpdatedEvent.Actor)]
               = [FuturesTradeSignalUpdatedEvent.Verb]
    };

    public async ValueTask StartAsync(
        Action<FuturesItiSignalV2ReadModel> trendDirectionChangedAction, 
        Action<FuturesItiSignalV2ReadModel> trendExtremeChangedAction, 
        Action<FuturesTradeSignalV2ReadModel> futuresTradeSignalAction)
    {
        await StartAsync(EventConsumer, _eventMap, EventHandlerAsync);

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            try
            {
                _ = eventVerb switch
                {
                    _ when eventVerb == FuturesItiSignalGeneratedEvent.Verb
                        => HandleGenerateEvent(eventMsg.AsEvent<FuturesItiSignalGeneratedEvent>()!),
                    _ when eventVerb == FuturesTradeSignalUpdatedEvent.Verb
                        => HandleUpdatedEvent(eventMsg.AsEvent<FuturesTradeSignalUpdatedEvent>()!),
                    _ => default!
                };
                await ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent(EventConsumer, ex, "EventHandlerAsync: failed while processing event verb: {EventVerb}", eventVerb);
            }

            IEvent HandleGenerateEvent(FuturesItiSignalGeneratedEvent e)
            {
                _ = e switch
                {
                    _ when e.FuturesItiSignal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged
                        => HandleSignalData(e.FuturesItiSignal, x => trendDirectionChangedAction?.Invoke(x)),
                    _ when e.FuturesItiSignal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
                        => HandleSignalData(e.FuturesItiSignal, x => trendExtremeChangedAction?.Invoke(x)),
                    _ => default!
                };
                return e;

                FuturesItiSignalV2ReadModel HandleSignalData(FuturesItiSignalV2ReadModel e, Action<FuturesItiSignalV2ReadModel> action)
                {
                    action?.Invoke(e);
                    return e;
                }
            }

            IEvent HandleUpdatedEvent(FuturesTradeSignalUpdatedEvent e)
            {
                futuresTradeSignalAction?.Invoke(e.FuturesTradeSignal);
                return e;
            }
        }
    }
}
