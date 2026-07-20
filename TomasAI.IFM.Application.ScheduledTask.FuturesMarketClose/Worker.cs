using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.EventQueue;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.Application.ServiceApi;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose
{
    public class Worker : BackgroundService
    {
        readonly IHost _host;
        readonly IApplicationCommandApi _appCommandApi;
        readonly ISystemAdminCommandApi _systemAdminCommandApi;
        readonly ISystemAdminQueryApi _systemAdminQueryApi;
        readonly ISystemAdminUIEventConsumer _systemAdminEventConsumer;
        readonly ILogger<Worker> _logger;

        /// <summary>
        /// start database backup worker process
        /// </summary>
        /// <param name="host"></param>
        /// <param name="logger"></param>
        /// <param name="appCommandApi"></param>
        /// <param name="systemAdminCommandApi"></param>
        /// <param name="systemAdminQueryApi"></param>
        /// <param name="systemAdminEventConsumer"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public Worker(
            IHost host,
            ILogger<Worker> logger,
            IApplicationCommandApi appCommandApi,
            ISystemAdminCommandApi systemAdminCommandApi,
            ISystemAdminQueryApi systemAdminQueryApi,
            ISystemAdminUIEventConsumer systemAdminEventConsumer)
        {
            _host = IsArgumentNull.Set(host);
            _logger = IsArgumentNull.Set(logger);
            _appCommandApi = IsArgumentNull.Set(appCommandApi);
            _systemAdminCommandApi = IsArgumentNull.Set(systemAdminCommandApi);
            _systemAdminQueryApi = IsArgumentNull.Set(systemAdminQueryApi);
            _systemAdminEventConsumer = IsArgumentNull.Set(systemAdminEventConsumer);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("shutting down IFM application services... ");
                await _appCommandApi.ShutdownApplicationAsync();
                await Task.Delay(TimeSpan.FromSeconds(2));
                _logger.LogInformation("loading database backup names...");
                var servicerResultDatabaseNames = await _systemAdminQueryApi.GetDatabaseNamesAsync();
                if (servicerResultDatabaseNames.Success)
                {
                    var infoMsgQueue = new ConcurrentEventQueue<string>(infoMsg => _logger.LogInformation(infoMsg));
                    infoMsgQueue.Start();
                    var databaseNames = servicerResultDatabaseNames.Value.Names;
                    var completedNames = new List<string>(databaseNames);
                    await _systemAdminEventConsumer.StartAsync(
                        backupAction: e => infoMsgQueue.EnqueueAndSignal($"{e.DatabaseName}: executing database backup"),
                        infoMsgAction: e => infoMsgQueue.EnqueueAndSignal($"{e.DatabaseName}: {e.InfoMessage}"),
                        completedAction: e => completedNames.Remove(e.DatabaseName),
                        failedAction: e => {
                            completedNames.Remove(e.DatabaseName);
                            infoMsgQueue.EnqueueAndSignal($"{e.DatabaseName}: database backup failed due to {e.ErrorMessage}");
                        });
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    foreach (var databaseName in databaseNames)
                    {
                        var backupType = DateTime.Now.DayOfWeek switch
                        {
                            DayOfWeek.Friday => DatabaseBackupType.Full,
                            DayOfWeek.Saturday => DatabaseBackupType.Full,
                            DayOfWeek.Sunday => DatabaseBackupType.Full,
                            _ => DatabaseBackupType.Diff
                        };
                          
                        var commandTimeout = DateTime.Now.DayOfWeek switch
                        {
                            DayOfWeek.Friday => TimeSpan.FromMinutes(60).TotalSeconds,
                            DayOfWeek.Saturday => TimeSpan.FromMinutes(60).TotalSeconds,
                            DayOfWeek.Sunday => TimeSpan.FromMinutes(60).TotalSeconds,
                            _ => TimeSpan.FromMinutes(15).TotalSeconds
                        };
                        var serviceResult = await _systemAdminCommandApi.BackupDatabaseAsync(databaseName, backupType, Convert.ToInt32(commandTimeout));
                        if (!serviceResult.Success)
                            _logger.LogInformation($"{databaseName}: database backup failed due to {serviceResult.ErrorMessage}");
                    }
                    while (completedNames.Count > 0)
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    await _systemAdminEventConsumer.StopAsync();
                    infoMsgQueue.Stop();   
                }
                else
                    _logger.LogError($"unable to load database backup names due to {servicerResultDatabaseNames.ErrorMessage}");
            }
            catch(Exception ex)
            {
                _logger.LogError($"unable to execute database backup due to {ex}");
            }
            finally
            {
                await _host.StopAsync();
            }
        }
    }
}
