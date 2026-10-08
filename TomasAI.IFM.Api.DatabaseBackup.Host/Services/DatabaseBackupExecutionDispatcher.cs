using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupExecutionDispatcher(
    IDatabaseBackupExecutionJournal journal,
    IDatabaseRecoveryOperationExecutor executor,
    DatabaseBackupHostOptions options,
    ILogger<DatabaseBackupExecutionDispatcher> logger, DatabaseBackupOperationOutput? output = null) : BackgroundService
{
    readonly Dictionary<DatabaseRecoveryOperationId, DateTimeOffset> _deferredUntil = [];

    public async ValueTask<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        var recoverable = new List<RecoverableJournalOperation>();
        await foreach (var operation in journal.ReadRecoverableOperationsAsync(cancellationToken).ConfigureAwait(false))
            recoverable.Add(operation);
        var count = 0;
        foreach (var operation in recoverable)
        {
            if (_deferredUntil.TryGetValue(operation.Intent.OperationId, out var deferredUntil)
                && deferredUntil > DateTimeOffset.UtcNow)
                continue;
            try
            {
                using var outputScope = new DatabaseRecoveryOutputScope(text => output?.Append(operation.Intent.Source, operation.Intent.OperationId.Value, text));
                output?.Append(operation.Intent.Source, operation.Intent.OperationId.Value, "Native execution attempt started.");
                await executor.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
                output?.Append(operation.Intent.Source, operation.Intent.OperationId.Value, "Native execution attempt returned; consult terminal phase for outcome.");
                _deferredUntil.Remove(operation.Intent.OperationId);
                count++;
            }
            catch (DatabaseLeaseLostException exception)
            {
                logger.LogWarning(exception, "A DatabaseBackup worker lost its fencing lease.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                output?.Append(operation.Intent.Source, operation.Intent.OperationId.Value, $"Execution exception: {exception.GetType().Name}; retry deferred.");
                _deferredUntil[operation.Intent.OperationId] = DateTimeOffset.UtcNow + options.FailedOperationRetryDelay;
                logger.LogError(
                    "DatabaseBackup operation {OperationId} for source {BackupSource} is degraded and will be retried after {RetryDelay}. FailureType={FailureType}; Message={FailureMessage}",
                    operation.Intent.OperationId.Format(), operation.Intent.Source, options.FailedOperationRetryDelay,
                    exception.GetType().Name, exception.Message);
            }
        }
        return count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        output?.PublishSetup();
        while (!stoppingToken.IsCancellationRequested)
        {
            var dispatched = await DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            if (dispatched == 0)
                await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
