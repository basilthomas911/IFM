using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public interface ICompositionRoutePlanStore
{
    Task SaveAsync(CompositionRoutePlan plan, CancellationToken cancellationToken);
    Task<CompositionRoutePlan?> ReadAsync(string planId, CancellationToken cancellationToken);
}
