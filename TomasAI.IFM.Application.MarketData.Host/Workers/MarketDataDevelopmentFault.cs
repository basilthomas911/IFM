using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Host;

public sealed partial class MarketDataWorkerHost
{
    internal async Task<bool> InjectDevelopmentFaultAsync(MarketDataDevelopmentFaultRequest fault, IHostEnvironment environment, CancellationToken token)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("MarketDataRecovery:DevelopmentVerification:Enabled"))
            throw new InvalidOperationException("Live fault injection requires explicitly enabled Development verification.");
        if (fault.OperationId == Guid.Empty) throw new InvalidDataException("Fault operation ID is required.");
        var worker = RequireWorker("Options", fault.WorkerInstanceId);
        var target = GetBackendStatus().Datasets.Single(dataset => dataset.Role == "Options" && dataset.Dataset == "GLBX.MDP3"
            && dataset.ValueDate == fault.ValueDate && dataset.WorkerInstanceId == fault.WorkerInstanceId
            && dataset.GenerationId == fault.GenerationId && dataset.Running);
        logger.LogWarning("Explicit Development Options-worker hang; OperationId={OperationId}; ProcessId={ProcessId}; GenerationId={GenerationId}", fault.OperationId, target.ProcessId, target.GenerationId);
        if (worker.Current.GenerationId != fault.GenerationId) throw new InvalidOperationException("Fault target generation changed.");
        await worker.HangAsync(token).ConfigureAwait(false);
        return true;
    }
}
