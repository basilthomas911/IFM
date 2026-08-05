using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes UI events related to spread distribution jobs and processes them using the specified service API.
/// </summary>
/// <remarks>This class subscribes to NATS events for spread distribution job submissions and executes the
/// corresponding jobs using the provided <see cref="ISpreadDistributionServiceApi"/>. It is designed to handle events
/// asynchronously and logs operations using the specified <see cref="ILogger"/>.</remarks>
public class SpreadDistributionJobUIEventConsumer
    : NatsActorEventListener, ISpreadDistributionJobUIEventConsumer
{
    readonly ISpreadDistributionServiceApi _spreadDistributionService;
    readonly ILogger _logger;
    readonly Guid _siteId = Guid.NewGuid();

    /// <summary>
    /// Creates a spread-distribution job UI event consumer.
    /// </summary>
    public SpreadDistributionJobUIEventConsumer(
        ISpreadDistributionServiceApi spreadDistributionService,
        INatsEventListenerOptions options,
        ILogger logger)
        : base(options, logger)
    {
        _spreadDistributionService = spreadDistributionService;
        _logger = logger;
    }

    public ValueTask StartAsync()
    {
        //await base.StartAsync();
        _logger.LogInformation("SpreadDistributionJobUIEventConsumer started.");
        return ValueTask.CompletedTask;
    }
   

}
