using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Framework.KafkaClient.UnitTests
{
    public class KafkaTestEventProducerOptions : KafkaEventProducerOptions
    {
        public KafkaTestEventProducerOptions(string bootstrapServers):base(bootstrapServers)
        {
        }
    }
}
