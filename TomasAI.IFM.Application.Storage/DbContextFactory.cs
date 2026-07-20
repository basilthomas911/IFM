using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;

namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// DbContext factory constructor
/// </summary>
/// <param name="dbContextResolver"></param>
public class DbContextFactory(IDbContextResolver dbContextResolver) : IDbContextFactory
{
    readonly IDbContextResolver _dbContextResolver = dbContextResolver;
    readonly Dictionary<Type, object> _dbContextPoolMap = [];

    // DbContext properties
    public IObjectRepository<EventSourceDbContext> EventSourceDb => _dbContextResolver.Resolve<EventSourceDbContext>();
    public IObjectRepository<EventSourceActorDbContext> ActorEventSourceDb => _dbContextResolver.Resolve<EventSourceActorDbContext>();
    public IObjectRepository<LogDbContext> LogDb => _dbContextResolver.Resolve<LogDbContext>();
    public IObjectRepository<SequenceIdDbContext> SequenceIdDb => _dbContextResolver.Resolve<SequenceIdDbContext>();
    public IFundDbContext FundDb => _dbContextResolver.Resolve<FundDbContext>() as IFundDbContext;
    public IMarketDataDbContext MarketDataDb => _dbContextResolver.Resolve<MarketDataDbContext>() as IMarketDataDbContext;
    public IOptionPricerDbContext OptionPricerDb => _dbContextResolver.Resolve<OptionPricerDbContext>() as IOptionPricerDbContext;
    public IObjectRepository<PredictiveModelDbContext> PredictiveModelDb => _dbContextResolver.Resolve<PredictiveModelDbContext>();
    public IReferenceDbContext ReferenceDb => _dbContextResolver.Resolve<ReferenceDbContext>() as IReferenceDbContext;
    public ISecuritiesDbContext SecuritiesDb => _dbContextResolver.Resolve<SecuritiesDbContext>() as ISecuritiesDbContext;
    public ITradeDbContext TradeDb => _dbContextResolver.Resolve<TradeDbContext>() as ITradeDbContext;
    public IYieldCurveRatesDbContext YieldCurveRatesDb => _dbContextResolver.Resolve<YieldCurveRatesDbContext>() as IYieldCurveRatesDbContext;
    public IObjectRepository<EconomicCalendarsDbContext> EconomicCalendarsDb => _dbContextResolver.Resolve<EconomicCalendarsDbContext>();

    public IDbContextPool<ReferenceDbContext> ReferencePool => GetPool<ReferenceDbContext>();

    public IObjectRepository<TRepo> Get<TRepo>() where TRepo : IObjectRepository
        => _dbContextResolver.Resolve<TRepo>();
  
    IDbContextPool<TRepo> GetPool<TRepo>() where TRepo : IObjectRepository
    {
        if (!_dbContextPoolMap.ContainsKey(typeof(TRepo)))
            _dbContextPoolMap.Add(typeof(TRepo), new DbContextPool<TRepo>(this));
        return (_dbContextPoolMap[typeof(TRepo)] as IDbContextPool<TRepo>)!;
    }

}
