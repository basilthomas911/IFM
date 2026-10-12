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
    Task HardResetAsync(LivePipelineHealthSnapshot unhealthySnapshot, CancellationToken token);
    Task RecoverDownstreamAsync(LivePipelineCheck unhealthyCheck, DateOnly valueDate, CancellationToken token);
}
