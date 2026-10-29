using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Recovery supervision is independent of the option queues and UI/broker notification operations.</summary>
public sealed class OptionPipelineRecoveryHostedService(OptionPipelineRecoveryCoordinator recovery,
    ILogger<OptionPipelineRecoveryHostedService> logger, IMarketDataApi? market = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            do
            {
                // Capability comes from the already-received backend snapshot.
                // Native lifecycle supervision must not depend on API polling.
                if (market?.DatasetRecoveryOwnership == MarketDataRecoveryOwnership.TokioDatasetOwner) return;
                try { recovery.Observe(stoppingToken); }
                catch (Exception error) { logger.LogError(error, "Option recovery supervision observation failed; observation will retry."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
