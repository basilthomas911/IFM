using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;

namespace TomasAI.IFM.Application.Storage.ScyllaDb
{
    public interface IDbContextFactory
    {
        IObjectRepository<FundDbContext> FundDb { get; }
        IObjectRepository<SecuritiesDbContext> SecuritiesDb { get; }
        IObjectRepository<MarketDataDbContext> MarketDataDb { get; }
        IObjectRepository<OptionPricerDbContext> OptionPricerDb { get; }
        IObjectRepository<ReferenceDbContext> ReferenceDb { get; }
        IObjectRepository<TradeDbContext> TradeDb { get; }
        IObjectRepository<YieldCurveRatesDbContext> YieldCurveRatesDb { get; }
        IObjectRepository<EconomicCalendarsDbContext> EconomicCalendarsDb { get; }
        IObjectRepository<PredictiveModelDbContext> PredictiveModelDb { get; }
    }
}
