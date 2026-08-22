using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupNativeCapabilityValidationService(
    IEnumerable<IDatabaseNativeCapabilityValidation> capabilities,
    DatabaseBackupHostHealthState health,
    IDatabaseBackupSourceHealthRegistry sourceHealth,
    ILogger<DatabaseBackupNativeCapabilityValidationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var capability in capabilities)
                await capability.ValidateAsync(cancellationToken).ConfigureAwait(false);
            var local = sourceHealth.Snapshot().SingleOrDefault(static source => source.Source == BackupSource.LocalWorkstation);
            sourceHealth.Set(BackupSource.LocalWorkstation, local?.Enabled ?? true, ready: true, "native-capabilities-qualified");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var local = sourceHealth.Snapshot().SingleOrDefault(static source => source.Source == BackupSource.LocalWorkstation);
            sourceHealth.Set(BackupSource.LocalWorkstation, local?.Enabled ?? true, ready: false,
                $"native-{exception.GetType().Name.ToLowerInvariant()}");
            logger.LogWarning(
                "Local database-backup native capabilities are unavailable. FailureType={FailureType}; Message={FailureMessage}",
                exception.GetType().Name, exception.Message);
        }
        health.MarkNativeCapabilitiesReady();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
