namespace TomasAI.IFM.Application.Api.Server;

public sealed class DeploymentIdentityEnforcementService(
    DeploymentIdentityMonitor monitor,
    IHostApplicationLifetime lifetime,
    ILogger<DeploymentIdentityEnforcementService> logger) : BackgroundService
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (EnforceOnce()) return;
        }
    }

    internal bool EnforceOnce()
    {
        var validation = monitor.Validate();
        if (validation.Valid) return false;

        logger.LogCritical(
            "Deployment identity changed after process startup; stopping stale API process. Errors: {Errors}",
            string.Join(" ", validation.Errors));
        lifetime.StopApplication();
        return true;
    }
}
