using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.YieldCurveRatesDb
{
    public interface  IYieldCurveRatesDbContext : IObjectRepository<YieldCurveRatesDbContext>
    {
        Task<ICollection<YieldCurveRateReadModel>> ReadAsync();
    }
}
