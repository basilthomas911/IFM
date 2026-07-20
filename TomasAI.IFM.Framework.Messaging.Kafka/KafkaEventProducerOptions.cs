using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Kafka
{
    public class KafkaEventProducerOptions : IEventProducerOptions
    {
        private readonly string _bootstrapServers;

        public KafkaEventProducerOptions(string bootstrapServers)
        {
            _bootstrapServers = bootstrapServers;
        }

        public string BootstrapServers => _bootstrapServers;

    }
}
