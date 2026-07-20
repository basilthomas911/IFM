using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Service.SystemAdmin.Backup;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.SystemAdmin.EventDataBackup.HostedService
{

    public class EventDataBackupEventConsumer : KafkaEventConsumer, IEventDataBackupEventConsumer
    {
        private readonly IDatabaseBackupServiceApi _dbBackupService;
        private readonly Guid _siteId;

        public EventDataBackupEventConsumer(IDatabaseBackupServiceApi dbBackupService, IEventConsumerOptions options, ILogger logger) :base(options, logger)
        {
            _dbBackupService = dbBackupService;
            _siteId = Guid.NewGuid();
        }

        protected override void ConnectEvents()
            => Subscribe($"{_siteId}", 
                new IEvent[] { new DatabaseBackupJobSubmittedEvent { }.SetEventSource($"{EventTopic.SystemAdminEvents}") }, 
                async e => await _dbBackupService.ExecuteAsync(e as DatabaseBackupJobSubmittedEvent));
    }
}
