using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Kafka
{
    /// <summary>
    /// kafka consumer configuration options
    /// </summary>
    public class KafkaEventConsumerOptions : IEventConsumerOptions
    {
        private readonly string _groupId;
        private readonly string _bootstrapServers;
        private readonly bool _enableAutoCommit;

        /// <summary>
        /// kafka consumer options constructor
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="bootstrapServers"></param>
        /// <param name="enableAutoCommit"></param>
        public KafkaEventConsumerOptions(string groupId, string bootstrapServers, bool enableAutoCommit)
        {
            _groupId = string.IsNullOrWhiteSpace(groupId) ? $"{Guid.NewGuid()}" : groupId;
            _bootstrapServers = bootstrapServers;
            _enableAutoCommit = enableAutoCommit;
        }

        /// <summary>
        /// kafka broker group id
        /// </summary>
        public string GroupId => _groupId;

        /// <summary>
        /// list of brokers we can connecto
        /// </summary>
        public string BootstrapServers => _bootstrapServers;

        /// <summary>
        /// auto commit after default period of time
        /// </summary>
        public bool EnableAutoCommit => _enableAutoCommit;
    }
}
