using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

/// <summary>
/// Runs the independent AWS journal/outbox/execution loop. AWS failures degrade only
/// the AWS source and never terminate the host or the local-workstation processor.
/// </summary>
public sealed class AwsDatabaseBackupRuntimeService(
    DynamoDbDatabaseBackupExecutionJournal journal,
    AwsCloudDatabaseRecoveryProcessor processor,
    IDatabaseBackupServiceEventTransport transport,
    IDatabaseBackupSourceHealthRegistry health,
    DatabaseBackupHostOptions hostOptions,
    AwsCloudDatabaseBackupOptions awsOptions,
    AwsDatabaseBackupTelemetry telemetry,
    ILogger<AwsDatabaseBackupRuntimeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialized = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!initialized)
                {
                    await journal.InitializeAsync(stoppingToken).ConfigureAwait(false);
                    await journal.VerifyIntegrityAsync(stoppingToken).ConfigureAwait(false);
                    initialized = true;
                    health.Set(BackupSource.AwsCloud, enabled: true, ready: true,
                        awsOptions.AcceptBackupRequests ? "ready" : "ready-admission-disabled");
                }

                var work = 0;
                var outboxBacklog = 0;
                await foreach (var pending in journal.ReadPendingServiceEventsAsync(hostOptions.OutboxBatchSize, stoppingToken).ConfigureAwait(false))
                {
                    outboxBacklog++;
                    await transport.PublishAsync(pending.Event, stoppingToken).ConfigureAwait(false);
                    await journal.MarkServiceEventPublishedAsync(pending.EventId, DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
                    work++;
                }
                telemetry.RecordOutboxBacklog(outboxBacklog);
                await foreach (var operation in journal.ReadRecoverableOperationsAsync(stoppingToken).ConfigureAwait(false))
                {
                    var age = DateTimeOffset.UtcNow - operation.Intent.ExecutionEvent.Source.ObservedUtc;
                    telemetry.RecordIntentAge(age < TimeSpan.Zero ? TimeSpan.Zero : age);
                    await processor.ExecuteAsync(operation, stoppingToken).ConfigureAwait(false);
                    work++;
                }
                if (work == 0) await Task.Delay(hostOptions.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                initialized = false;
                telemetry.RecordRuntimeFailure(AwsFailureCategory(exception));
                health.Set(BackupSource.AwsCloud, enabled: true, ready: false,
                    $"degraded-{exception.GetType().Name}");
                logger.LogWarning("AWS DatabaseBackup runtime is degraded and will retry. FailureType={FailureType}; Message={FailureMessage}",
                    exception.GetType().Name, exception.Message);
                await Task.Delay(hostOptions.FailedOperationRetryDelay, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    static string AwsFailureCategory(Exception exception) => exception switch
    {
        TimeoutException => "timeout",
        UnauthorizedAccessException => "authorization",
        InvalidDataException => "verification",
        _ => "dependency"
    };
}
