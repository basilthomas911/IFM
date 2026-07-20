using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Shared.SystemAdmin.Events;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Service.SystemAdmin.Backup;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.SystemAdmin.HostedService
{
    public class DatabaseBackupEventConsumer : KafkaEventConsumer, IDatabaseBackupEventConsumer
    {
        private readonly IDatabaseBackupService _databaseBackupService;
        private readonly Guid _siteId;

        public DatabaseBackupEventConsumer(IDatabaseBackupService databaseBackupService, IEventConsumerOptions options, ILogger<DatabaseBackupEventConsumer> logger) :base(options, logger)
        {
            _databaseBackupService = databaseBackupService;
            _siteId = Guid.NewGuid();
        }

        protected override void ConnectEvents()
            => Subscribe($"{_siteId}", 
                new IEvent[] { new DatabaseBackupEvent { }.SetEventSource($"{EventTopic.SystemAdminEvents}") }, 
                async e => await _databaseBackupService.ExecuteAsync(e as DatabaseBackupEvent));
    }
}
