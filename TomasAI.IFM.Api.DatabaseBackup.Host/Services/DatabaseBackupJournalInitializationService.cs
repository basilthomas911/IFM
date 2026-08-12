using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupJournalInitializationService(
    IDatabaseBackupExecutionJournal journal,
    DatabaseBackupHostHealthState health) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await journal.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await journal.VerifyIntegrityAsync(cancellationToken).ConfigureAwait(false);
        health.MarkJournalReady();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
