using System;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.KafkaClient.UnitTests;

/// <summary>
/// tets consumer
/// </summary>
public class KafkaTestConsumer(IEventConsumerOptions options, ILogger<KafkaTestConsumer> logger) 
    : KafkaEventConsumer(options, logger)
{
    Guid _siteId = Guid.NewGuid();
    Action<MarketDataFeedStartedEvent> _consoleWriter;

    public void SetConsoleWrtiter(Action<MarketDataFeedStartedEvent> consoleWriter) => _consoleWriter = IsArgumentNull.Set(consoleWriter);

    protected override void ConnectEvents()
        => Subscribe($"{_siteId}", 
            [new MarketDataFeedStartedEvent { }.SetEventSource($"{EventTopic.MarketDataFeedEvents}") ], 
            e => _consoleWriter(e as MarketDataFeedStartedEvent));
}
