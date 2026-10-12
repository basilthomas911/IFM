using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;

public interface IDurableCompositionReconciler
{
    Task<DurableRealization?> ReconcileOnceAsync(CancellationToken cancellationToken);
}
