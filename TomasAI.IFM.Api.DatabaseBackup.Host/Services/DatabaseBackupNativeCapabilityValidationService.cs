using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupNativeCapabilityValidationService(
    IEnumerable<IDatabaseNativeCapabilityValidation> capabilities,
    DatabaseBackupHostHealthState health) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var capability in capabilities)
            await capability.ValidateAsync(cancellationToken).ConfigureAwait(false);
        health.MarkNativeCapabilitiesReady();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
