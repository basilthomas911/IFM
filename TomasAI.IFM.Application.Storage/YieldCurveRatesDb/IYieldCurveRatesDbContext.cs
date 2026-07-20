using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Storage.YieldCurveRatesDb
{
    public interface  IYieldCurveRatesDbContext : IObjectRepository<YieldCurveRatesDbContext>
    {
        Task<ICollection<YieldCurveRateReadModel>> ReadAsync();
    }
}
