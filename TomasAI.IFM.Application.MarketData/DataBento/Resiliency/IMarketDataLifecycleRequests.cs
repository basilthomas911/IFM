using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface IMarketDataLifecycleRequests
{
    DatabentoLifecycleSnapshot Current { get; }
    Task StartAsync(DateOnly valueDate, Func<Guid, int, string, Task>? errorMessageHandler = null, CancellationToken cancellationToken = default);
    Task StopAsync(DateOnly valueDate, CancellationToken cancellationToken = default);
    Task ResetAsync(DateOnly valueDate, Guid correlationId, CancellationToken cancellationToken = default);
    Task ProbeAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
