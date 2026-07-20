using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.SystemAdmin.HostedService
{
    public class SystemAdminHostedService : IHostedService
    {
        private readonly IDatabaseBackupEventConsumer _databaseBackupEventConsumer;

        public SystemAdminHostedService(IDatabaseBackupEventConsumer databaseBackupEventConsumer)
        {
            _databaseBackupEventConsumer = databaseBackupEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _databaseBackupEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _databaseBackupEventConsumer.StopAsync();

    }
}
