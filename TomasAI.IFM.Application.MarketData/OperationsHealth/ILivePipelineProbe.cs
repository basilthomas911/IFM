using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public interface ILivePipelineProbe
{
    Task<LivePipelineHealthSnapshot> CheckAsync(CancellationToken token);
    /// <summary>Requests the existing fatal API shutdown after required downstream recovery is exhausted.</summary>
    Task ShutdownAsync(LivePipelineHealthSnapshot unhealthySnapshot, int hardResetAttempts, CancellationToken token)
        => Task.FromException(new InvalidOperationException("Fatal pipeline shutdown is not configured."));
    Task HardResetAsync(LivePipelineHealthSnapshot unhealthySnapshot, CancellationToken token);
    Task RecoverDownstreamAsync(LivePipelineCheck unhealthyCheck, DateOnly valueDate, CancellationToken token);
}
