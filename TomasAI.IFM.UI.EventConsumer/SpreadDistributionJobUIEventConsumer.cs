using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;

namespace TomasAI.IFM.UI.EventConsumer;

/// <summary>
/// Consumes UI events related to spread distribution jobs and processes them using the specified service API.
/// </summary>
/// <remarks>This class subscribes to NATS events for spread distribution job submissions and executes the
/// corresponding jobs using the provided <see cref="ISpreadDistributionServiceApi"/>. It is designed to handle events
/// asynchronously and logs operations using the specified <see cref="ILogger"/>.</remarks>
/// <param name="spreadDistributionService"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public class SpreadDistributionJobUIEventConsumer(ISpreadDistributionServiceApi spreadDistributionService, INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), ISpreadDistributionJobUIEventConsumer
{
    readonly ISpreadDistributionServiceApi _spreadDistributionService = spreadDistributionService;
    readonly Guid _siteId = Guid.NewGuid();

    public async ValueTask StartAsync()
    {
        //await base.StartAsync();
        logger.LogInformation("SpreadDistributionJobUIEventConsumer started.");
    }
   

}
