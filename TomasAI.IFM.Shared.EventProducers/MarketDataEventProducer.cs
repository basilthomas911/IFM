using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.Events;


namespace TomasAI.IFM.Shared.EventProducers;

/// <summary>
/// Produces and publishes market data events through NATS and NATS JetStream.
/// </summary>
/// <remarks>This class specializes <see cref="NatsEventProducer"/> to handle various market data event types,
/// such as futures contracts and yield curve rates. Production events are sent through core NATS and JetStream;
/// a parameterless constructor is available for test scenarios.</remarks>
public class MarketDataEventProducer : NatsEventProducer, IMarketDataEventProducer
{
    /// <summary>
    /// market data event producer
    /// </summary>
    /// <param name="options"></param>
    /// <param name="jetStreamOptions"></param>
    /// <param name="loggerFactory"></param>
    public MarketDataEventProducer(
        INatsProducerOptions options,
        INatsJetStreamProducerOptions jetStreamOptions,
        ILoggerFactory loggerFactory)
        : base(options, jetStreamOptions, loggerFactory)
    {
    }

    /// <summary>
    /// for BDD Test usage only
    /// </summary>
    public MarketDataEventProducer()
    { 
    }

    /// <summary>
    /// post market data events to event broker
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    public override async Task PostEventAsync(IEvent @event)
    {
        @event.SetEventSource($"{EventTopic.MarketDataEvents}");
        await (@event switch
        {
            // futures contract added events...
            FuturesContractAddedEvent e => SendEventAsync(e.Contract.ContractId, e),
            FuturesContractAddedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesContractAddedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures contract changed events...
            FuturesContractChangedEvent e => SendEventAsync(e.OriginalContractId, e),
            FuturesContractChangedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesContractChangedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures contract removed events...
            FuturesContractRemovedEvent e => SendEventAsync(e.ContractId, e),
            FuturesContractRemovedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesContractRemovedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures option contract added events...
            FuturesOptionContractAddedEvent e => SendEventAsync(e.Contract.ContractId, e),
            FuturesOptionContractAddedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionContractAddedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures option contract changed events..
            FuturesOptionContractChangedEvent e => SendEventAsync(e.OriginalContractId, e),
            FuturesOptionContractChangedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionContractChangedFailEvent e => SendEventAsync(e.CommandId, e),

            // futures option contract removed events...
            FuturesOptionContractRemovedEvent e => SendEventAsync(e.ContractId, e),
            FuturesOptionContractRemovedCompleteEvent e => SendEventAsync(e.CommandId, e),
            FuturesOptionContractRemovedFailEvent e => SendEventAsync(e.CommandId, e),

            // yield curve rate chaged events...
            YieldCurveRateChangedEvent e => SendEventAsync($"{e.UpdatedOn:yyyy-MM-dd}", e),
            YieldCurveRateChangedCompleteEvent e => SendEventAsync(e.CommandId, e),
            YieldCurveRateChangedFailEvent e => SendEventAsync(e.CommandId, e),

            // yield curve rate removed events...
            YieldCurveRateRemovedEvent e => SendEventAsync($"{e.ValueDate:yyyy-MM-dd}", e),
            YieldCurveRateRemovedCompleteEvent e => SendEventAsync(e.CommandId, e),
            YieldCurveRateRemovedFailEvent e => SendEventAsync(e.CommandId, e),

            // yield curve rate added events...
            YieldCurveRateAddedEvent e => SendEventAsync($"{e.CreatedOn:yyyy-MM-dd}", e),
            YieldCurveRateAddedCompleteEvent e => SendEventAsync(e.CommandId, e),
            YieldCurveRateAddedFailEvent e => SendEventAsync(e.CommandId, e),

            // yield curve rates imported events...
            YieldCurveRatesImportedEvent e => SendEventAsync(e.CommandId, e),
            YieldCurveRatesImportedCompleteEvent e => SendEventAsync(e.CommandId, e),
            YieldCurveRatesImportedFailEvent e => SendEventAsync(e.CommandId, e),

            // denormalizer events removed: types not available in this project
            StatusConsoleLoggedEvent e => SendEventAsync(e.CommandId, e),
            CommandExceptionEvent e => SendEventAsync(e.CommandId, e),
            _ => Task.CompletedTask
        });
    }

}
