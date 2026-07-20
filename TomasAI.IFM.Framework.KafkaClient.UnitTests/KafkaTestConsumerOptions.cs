using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Framework.KafkaClient.UnitTests
{
    public class KafkaTestEventConsumerOptions : KafkaEventConsumerOptions
    {
        public KafkaTestEventConsumerOptions(string groupId, string bootstrapServers, bool enableAutoCommit) 
            : base(groupId, bootstrapServers, enableAutoCommit)
        {
        }
    }
}
