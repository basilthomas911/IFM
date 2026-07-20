using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Framework.Messaging.Kafka;

namespace TomasAI.IFM.Service.OptionPricer.HostedService;

/// <summary>
/// Consumes option pricer events from a Kafka event broker and processes spread distribution job submissions.
/// </summary>
/// <remarks>This class subscribes to option pricer event topics and handles incoming spread distribution job
/// events by invoking the associated spread distribution service. It is typically used in systems that require
/// automated processing of option pricing workflows triggered by event streams. Thread safety and lifecycle management
/// are inherited from the base KafkaEventConsumer implementation.</remarks>
public class OptionPricerEventConsumer : KafkaEventConsumer, IOptionPricerEventConsumer
{
    private readonly ISpreadDistributionServiceApi _spreadDistributionService;
    private readonly Guid _siteId;

    /// <summary>
    /// option pricer event consumer constructor
    /// </summary>
    /// <param name="spreadDistributionService"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public OptionPricerEventConsumer(
        ISpreadDistributionServiceApi spreadDistributionService, 
        IEventConsumerOptions options, 
        ILogger logger) :base(options, logger)
    {
        _spreadDistributionService = spreadDistributionService;
        _siteId = Guid.NewGuid();
        logger.LogInformation("OptionPricerEventConsumer started");
    }

    /// <summary>
    /// consume option pricer events from event broker
    /// </summary>
    protected override void ConnectEvents()
    {
        var consumeEvents = new List<IEvent> {
            new SpreadDistributionJobSubmittedEvent{ },
        };
        consumeEvents.ForEach(e => e.SetEventSource($"{EventTopic.OptionPricerEvents}"));
        Subscribe($"{_siteId}", consumeEvents,  async e => await HandleEventAsync(e));
    }

    /// <summary>
    /// handle spread dsitribution job submitted event
    /// </summary>
    /// <param name="event"></param>
    /// <returns></returns>
    async Task HandleEventAsync(IEvent @event)
    {
        var e = @event as SpreadDistributionJobSubmittedEvent;
        if (e is not null)
            await _spreadDistributionService.ExecuteAsync(e.SpreadDistributionJob);
    }
}
