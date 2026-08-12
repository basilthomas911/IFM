using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupExecutionDispatcher(
    IDatabaseBackupExecutionJournal journal,
    IDatabaseRecoveryOperationExecutor executor,
    LocalWorkstationDatabaseBackupOptions options,
    ILogger<DatabaseBackupExecutionDispatcher> logger) : BackgroundService
{
    public async ValueTask<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        var recoverable = new List<RecoverableJournalOperation>();
        await foreach (var operation in journal.ReadRecoverableOperationsAsync(cancellationToken).ConfigureAwait(false))
            recoverable.Add(operation);
        var count = 0;
        foreach (var operation in recoverable)
        {
            try
            {
                await executor.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
                count++;
            }
            catch (DatabaseLeaseLostException exception)
            {
                logger.LogWarning(exception, "A DatabaseBackup worker lost its fencing lease.");
            }
        }
        return count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var dispatched = await DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            if (dispatched == 0)
                await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
