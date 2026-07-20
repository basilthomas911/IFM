using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.SystemAdmin.EventDataBackup.HostedService
{
    public class EventDataBackupHostedService : IHostedService
    {
        private readonly IEventDataBackupEventConsumer _eventDataBackupEventConsumer;

        public EventDataBackupHostedService(IEventDataBackupEventConsumer eventDataBackupEventConsumer)
        {
            _eventDataBackupEventConsumer = eventDataBackupEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _eventDataBackupEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _eventDataBackupEventConsumer.StopAsync();

    }
}
