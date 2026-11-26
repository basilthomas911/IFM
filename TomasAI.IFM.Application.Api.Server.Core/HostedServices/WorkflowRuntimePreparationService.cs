using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Host startup awaits process-local code readiness before new local decisions may be admitted.</summary>
public sealed class WorkflowRuntimePreparationService(WorkflowRuntimePreparation preparation,
    IntrinsicTimeStrategyWorkflowOptions options,ILogger<WorkflowRuntimePreparationService> logger):IHostedLifecycleService,IHealthCheck
{
    // Host completes every StartingAsync before invoking any hosted StartAsync,
    // including when ServicesStartConcurrently is enabled. Producers therefore
    // cannot race the temporary unprepared admission fence.
    public Task StartingAsync(CancellationToken cancellationToken)=>StartAsync(cancellationToken);
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if(!options.LocalDecisionRuntimeEnabled||preparation.IsReady)return;
        var result=await preparation.PrepareAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Workflow runtime prepared {Methods} methods and {Contracts} contracts in {Milliseconds} ms before decision readiness.",
            result.PreparedMethods,result.PreparedContracts,result.Elapsed.TotalMilliseconds);
    }
    public Task StopAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)
        =>Task.FromResult(!options.LocalDecisionRuntimeEnabled?HealthCheckResult.Healthy("Local workflow runtime is disabled."):
            preparation.IsReady?HealthCheckResult.Healthy("Workflow code and serialization prepared before decision admission."):
            HealthCheckResult.Unhealthy(preparation.Failure??"Workflow code and serialization preparation has not completed."));
}
