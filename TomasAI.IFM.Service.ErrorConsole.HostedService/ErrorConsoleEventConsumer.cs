using System;
using System.Collections.Generic;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging.Kafka;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Service.ErrorConsole.HostedService;

/// <summary>
/// error console event consumer
/// </summary>
/// <param name="errorConsoleService"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public class ErrorConsoleEventConsumer(IErrorConsoleService errorConsoleService, IEventConsumerOptions options, ILogger<ErrorConsoleEventConsumer> logger)
        : KafkaEventConsumer(options, logger), IErrorConsoleEventConsumer
{
    readonly IErrorConsoleService _errorConsoleService = errorConsoleService;
    readonly Guid _siteId = Guid.NewGuid();

    /// <summary>
    /// consume only exception events from event broker
    /// </summary>
    protected override void ConnectEvents()
    {
        var @events = new List<IEvent>
        {
            new CommandExceptionEvent{ },
            new QueryExceptionEvent{ },
            new EventServiceExceptionEvent{ },
        };
        @events.ForEach(e => e.SetEventSource($"{EventTopic.ExceptionEvents}"));
        Subscribe($"{_siteId}", events, e => _errorConsoleService.ExecuteAsync((dynamic)e));
     }
}
