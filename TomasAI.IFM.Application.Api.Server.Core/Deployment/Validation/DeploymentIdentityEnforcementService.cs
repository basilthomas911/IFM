using TomasAI.IFM.Application.Api.Server.Core.Deployment.Identity;
namespace TomasAI.IFM.Application.Api.Server.Core.Deployment.Validation;

public sealed class DeploymentIdentityEnforcementService(
    DeploymentIdentityMonitor monitor,
    IHostApplicationLifetime lifetime,
    TimeProvider timeProvider,
    ILogger<DeploymentIdentityEnforcementService> logger) : BackgroundService
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(PollInterval, timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                if (EnforceOnce()) return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    internal bool EnforceOnce()
    {
        var validation = monitor.Validate();
        if (validation.Valid) return false;

        logger.LogCritical(
            "{Component}.{Method} "+"Deployment identity changed after process startup; stopping stale API process. Errors: {Errors}",nameof(DeploymentIdentityEnforcementService),nameof(EnforceOnce),            string.Join(" ", validation.Errors));
        lifetime.StopApplication();
        return true;
    }
}
