using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

/// <summary>Periodically exports bounded AWS database-backup telemetry without affecting source availability.</summary>
public sealed class AwsCloudWatchMetricExportService(
    AwsCloudWatchMetricExporter exporter,
    AwsCloudDatabaseBackupOptions options,
    ILogger<AwsCloudWatchMetricExportService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.CloudWatchExportInterval);
        do
        {
            try
            {
                var count = await exporter.ExportPendingAsync(stoppingToken).ConfigureAwait(false);
                if (count > 0) logger.LogInformation("Exported {MetricCount} AWS backup metric observations.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                var failure = AwsFailureClassifier.Classify(exception);
                logger.LogWarning(
                    "AWS backup metric export failed safely. Kind={FailureKind}; Code={FailureCode}; Retryable={Retryable}",
                    failure.Kind, failure.Code, failure.Retryable);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
