using TomasAI.IFM.Application.DatabaseBackup.Adapters.AwsCloud.Journal;
using TomasAI.IFM.Application.DatabaseBackup.Adapters.AwsCloud.Processing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
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
    AwsAutomaticRetentionCommandSender retentionCommands,
    ILogger<AwsDatabaseBackupRuntimeService> logger, DatabaseBackupOperationOutput? output = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialized = false;
        var nextRetentionCheckUtc = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!initialized)
                {
                    await journal.InitializeAsync(stoppingToken).ConfigureAwait(false);
                    await journal.VerifyIntegrityAsync(stoppingToken).ConfigureAwait(false);
                    initialized = true;
                    health.Set(BackupSource.AwsCloud, true, true, awsOptions.AcceptBackupRequests ? "ready" : "ready-admission-disabled");
                }
                var work = 0;
                var backlog = 0;
                await foreach (var pending in journal.ReadPendingServiceEventsAsync(hostOptions.OutboxBatchSize, stoppingToken).ConfigureAwait(false))
                {
                    backlog++;
                    await transport.PublishAsync(pending.Event, stoppingToken).ConfigureAwait(false);
                    await journal.MarkServiceEventPublishedAsync(pending.EventId, DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
                    work++;
                }
                telemetry.RecordOutboxBacklog(backlog);
                var pendingOperations = new List<RecoverableJournalOperation>();
                await foreach (var operation in journal.ReadRecoverableOperationsAsync(stoppingToken).ConfigureAwait(false))
                    pendingOperations.Add(operation);
                foreach (var operation in SelectRunnableOperations(pendingOperations, hostOptions.CutoverWorkerOnly))
                {
                    var age = DateTimeOffset.UtcNow - operation.Intent.ExecutionEvent.Source.ObservedUtc;
                    telemetry.RecordIntentAge(age < TimeSpan.Zero ? TimeSpan.Zero : age);
                    logger.LogInformation("AWS operation {OperationId} executing. Kind={Kind}; ProtectionSet={ProtectionSet}",
                        operation.Intent.OperationId.Format(), operation.Intent.ExecutionEvent.Source.OperationKind,
                        operation.Intent.ExecutionEvent.Source.ProtectionSetId.Value);
                    try
                    {
                        using var outputScope = new DatabaseRecoveryOutputScope(text => output?.Append(BackupSource.AwsCloud, operation.Intent.OperationId.Value, text));
                        output?.Append(BackupSource.AwsCloud, operation.Intent.OperationId.Value, "AWS execution attempt started.");
                        await processor.ExecuteAsync(operation, stoppingToken).ConfigureAwait(false);
                        output?.Append(BackupSource.AwsCloud, operation.Intent.OperationId.Value, "AWS execution attempt returned; consult terminal phase for outcome.");
                        logger.LogInformation("AWS operation {OperationId} execution returned.", operation.Intent.OperationId.Format());
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException
                        && operation.Intent.ExecutionEvent.Source.OperationKind == DatabaseRecoveryOperationKind.Retention)
                    {
                        output?.Append(BackupSource.AwsCloud, operation.Intent.OperationId.Value, $"Execution exception: {exception.GetType().Name}.");
                        logger.LogError(exception,
                            "AWS retention {OperationId} failed. ProtectionSet={ProtectionSet}; PlanId={PlanId}; PlanRevision={PlanRevision}; backup processing continues.",
                            operation.Intent.OperationId.Format(), operation.Intent.ExecutionEvent.Source.ProtectionSetId.Value,
                            operation.Intent.ExecutionEvent.RetentionPlanId, operation.Intent.ExecutionEvent.RetentionPlanRevision);
                    }
                    catch (Exception exception)
                    {
                        output?.Append(BackupSource.AwsCloud, operation.Intent.OperationId.Value,
                            $"Execution exception: {exception.GetType().Name}; consult runtime diagnostics.");
                        throw;
                    }
                    work++;
                }
                if (!hostOptions.CutoverWorkerOnly && work == 0 && DateTimeOffset.UtcNow >= nextRetentionCheckUtc)
                {
                    var scheduleAccepted = true;
                    foreach (var intent in AwsAutomaticRetentionSchedule.Create(awsOptions, DateTimeOffset.UtcNow))
                    {
                        try { await retentionCommands.SendAsync(intent, stoppingToken).ConfigureAwait(false); }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            logger.LogWarning(exception, "Automatic retention awaits the System Admin command actor. Backup/restore processing remains available.");
                            scheduleAccepted = false;
                            break;
                        }
                        logger.LogInformation("Automatic AWS retention {OperationId} admission {Outcome}. ProtectionSet={ProtectionSet}; Replica={Replica}; Boundary={Boundary}",
                            intent.OperationId.Format(), "CommandAccepted", intent.ExecutionEvent.Source.ProtectionSetId.Value,
                            intent.ExecutionEvent.RequiredDestinations[0].Name, intent.ExecutionEvent.EvaluationBoundaryUtc);
                    }
                    nextRetentionCheckUtc = DateTimeOffset.UtcNow + (scheduleAccepted ? awsOptions.AutomaticRetentionInterval : hostOptions.FailedOperationRetryDelay);
                }
                if (work == 0) await Task.Delay(hostOptions.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                initialized = false;
                telemetry.RecordRuntimeFailure(AwsFailureCategory(exception));
                health.Set(BackupSource.AwsCloud, true, false, $"degraded-{exception.GetType().Name}");
                logger.LogWarning(exception, "AWS backup runtime will retry. FailureType={FailureType}; Message={FailureMessage}",
                    exception.GetType().Name, exception.Message);
                await Task.Delay(hostOptions.FailedOperationRetryDelay, stoppingToken).ConfigureAwait(false);
            }
        }
    }
    /// <summary>
    /// Keeps retention admitted, without acquiring a lease or starting it, while any
    /// backup, restore or cutover is pending. Re-evaluate after those jobs complete.
    /// </summary>
    internal static IReadOnlyList<RecoverableJournalOperation> SelectRunnableOperations(
        IReadOnlyList<RecoverableJournalOperation> pending, bool cutoverWorkerOnly)
    {
        var busy = pending.Any(operation =>
            operation.Intent.ExecutionEvent.Source.OperationKind != DatabaseRecoveryOperationKind.Retention);
        return pending.Where(operation =>
        {
            var kind = operation.Intent.ExecutionEvent.Source.OperationKind;
            return (kind == DatabaseRecoveryOperationKind.Cutover) == cutoverWorkerOnly
                && (kind != DatabaseRecoveryOperationKind.Retention || !busy);
        }).ToArray();
    }
    static string AwsFailureCategory(Exception exception) => exception switch
    {
        TimeoutException => "timeout",
        UnauthorizedAccessException => "authorization",
        InvalidDataException => "verification",
        _ => "dependency"
    };
}
