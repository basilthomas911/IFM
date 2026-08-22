using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class AwsCloudIdentityValidationService(
    AwsCloudDatabaseBackupOptions options,
    IServiceProvider services,
    IDatabaseBackupSourceHealthRegistry health,
    ILogger<AwsCloudIdentityValidationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            health.Set(BackupSource.AwsCloud, enabled: false, ready: true, "disabled");
            return;
        }
        var preflight = services.GetService(typeof(IAwsIdentityPreflight)) as IAwsIdentityPreflight;
        if (preflight is null)
        {
            health.Set(BackupSource.AwsCloud, enabled: true, ready: false, "identity-preflight-unavailable");
            return;
        }
        try
        {
            _ = await preflight.VerifyAsync(cancellationToken).ConfigureAwait(false);
            health.Set(BackupSource.AwsCloud, enabled: true, ready: true, "identity-qualified");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = AwsFailureClassifier.Classify(exception);
            health.Set(BackupSource.AwsCloud, enabled: true, ready: false, $"identity-{failure.Kind.ToString().ToLowerInvariant()}");
            logger.LogWarning(
                "AWS backup identity preflight is unavailable. Kind={FailureKind}; Code={FailureCode}; RequestId={AwsRequestId}",
                failure.Kind, failure.Code, failure.RequestId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
