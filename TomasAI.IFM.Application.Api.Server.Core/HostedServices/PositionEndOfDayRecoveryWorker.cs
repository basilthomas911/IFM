using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Drains durable closing-quote recovery jobs independently of the readiness audit.</summary>
public sealed class PositionEndOfDayRecoveryWorker(PositionEndOfDayRecovery recovery, IActorSupervisor supervisor,
    ILogger<PositionEndOfDayRecoveryWorker> logger, TimeProvider clock) : BackgroundService, IHealthCheck
{
    sealed record State(DateTimeOffset? PolledAt,string Error,int? Pending);
    State state=new(null,string.Empty,null);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            do
            {
                if(!supervisor.IsReady)continue;
                try {
                    var pending=await recovery.GetOutstandingCountAsync(stoppingToken).ConfigureAwait(false);
                    Volatile.Write(ref state,new(clock.GetUtcNow(),Volatile.Read(ref state).Error,pending));
                    await Task.WhenAll(Enumerable.Range(0,4).Select(_=>recovery.RunOneAsync(stoppingToken))).ConfigureAwait(false);
                    Volatile.Write(ref state,new(clock.GetUtcNow(),string.Empty,await recovery.GetOutstandingCountAsync(stoppingToken).ConfigureAwait(false)));
                }
                catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { throw; }
                catch(Exception error) {
                    Volatile.Write(ref state,new(clock.GetUtcNow(),error.Message,Volatile.Read(ref state).Pending));
                    logger.LogError(error,"Closing-quote recovery worker failed; durable jobs remain retryable.");
                }
            } while(await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)
    {
        var current=Volatile.Read(ref state);
        if(current.Error.Length>0)return Task.FromResult(HealthCheckResult.Unhealthy("Closing-quote recovery worker failed: "+current.Error));
        if(current.PolledAt is null || current.Pending is null || clock.GetUtcNow()-current.PolledAt>TimeSpan.FromMinutes(3))
            return Task.FromResult(HealthCheckResult.Degraded("Closing-quote recovery worker has not completed startup or is stalled."));
        if(current.Pending>0)return Task.FromResult(HealthCheckResult.Unhealthy($"{current.Pending} closing-quote recovery jobs for open positions remain unfinished; authentic closing evidence is not yet qualified."));
        return Task.FromResult(HealthCheckResult.Healthy("Durable closing-quote recovery worker is polling; no unfinished recoveries for open positions."));
    }
}
