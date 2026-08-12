using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupStartupReconciliationService(
    IDatabaseBackupExecutionJournal journal,
    DatabaseBackupHostHealthState health,
    ILogger<DatabaseBackupStartupReconciliationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var _ in journal.ReadRecoverableOperationsAsync(cancellationToken).ConfigureAwait(false)) count++;
        health.SetRecoverableOperationCount(count);
        logger.LogInformation("Database Backup Host found {RecoverableOperationCount} recoverable journal operations.", count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
