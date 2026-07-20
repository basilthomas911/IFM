using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

namespace TomasAI.IFM.Application.Storage.ScyllaDb
{
    /// <summary>
    /// dbcontext factory
    /// </summary>
    public class DbContextFactory : IDbContextFactory
    {
        readonly IDbContextResolver _dbContextResolver;
 
        /// <summary>
        /// dbcontext factory constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        public DbContextFactory(IDbContextResolver dbContextResolver)
        {
            _dbContextResolver = dbContextResolver;
        }

        // dbcontext properties
        public IObjectRepository<FundDbContext> FundDb => _dbContextResolver.Resolve<FundDbContext>();
        public IObjectRepository<MarketDataDbContext> MarketDataDb => _dbContextResolver.Resolve<MarketDataDbContext>();
        public IObjectRepository<OptionPricerDbContext> OptionPricerDb => _dbContextResolver.Resolve<OptionPricerDbContext>();
        public IObjectRepository<ReferenceDbContext> ReferenceDb => _dbContextResolver.Resolve<ReferenceDbContext>();
        public IObjectRepository<SecuritiesDbContext> SecuritiesDb => _dbContextResolver.Resolve<SecuritiesDbContext>();
        public IObjectRepository<TradeDbContext> TradeDb => _dbContextResolver.Resolve<TradeDbContext>();
        public IObjectRepository<YieldCurveRatesDbContext> YieldCurveRatesDb => _dbContextResolver.Resolve<YieldCurveRatesDbContext>();
        public IObjectRepository<EconomicCalendarsDbContext> EconomicCalendarsDb => _dbContextResolver.Resolve<EconomicCalendarsDbContext>();
        public IObjectRepository<PredictiveModelDbContext> PredictiveModelDb => _dbContextResolver.Resolve<PredictiveModelDbContext>();

    }
}
