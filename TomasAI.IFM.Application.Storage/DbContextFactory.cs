using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.PredictiveModelDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.FundDb.Schema;
using TomasAI.IFM.Application.Storage.LogDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
using TomasAI.IFM.Application.Storage.PredictiveModelDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;

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
    public ISystemAdminDbContext SystemAdminDb => _dbContextResolver.Resolve<SystemAdminDbContext>() as ISystemAdminDbContext;

    public EventSourceSchemaDb EventSourceSchema => (_dbContextResolver.Resolve<EventSourceSchemaDb>() as EventSourceSchemaDb)!;
    public LogSchemaDb LogSchema => (_dbContextResolver.Resolve<LogSchemaDb>() as LogSchemaDb)!;
    public SequenceIdSchemaDb SequenceIdSchema => (_dbContextResolver.Resolve<SequenceIdSchemaDb>() as SequenceIdSchemaDb)!;
    public FundSchemaDb FundSchema => (_dbContextResolver.Resolve<FundSchemaDb>() as FundSchemaDb)!;
    public MarketDataSchemaDb MarketDataSchema => (_dbContextResolver.Resolve<MarketDataSchemaDb>() as MarketDataSchemaDb)!;
    public OptionPricerSchemaDb OptionPricerSchema => (_dbContextResolver.Resolve<OptionPricerSchemaDb>() as OptionPricerSchemaDb)!;
    public PredictiveModelSchemaDb PredictiveModelSchema => (_dbContextResolver.Resolve<PredictiveModelSchemaDb>() as PredictiveModelSchemaDb)!;
    public ReferenceSchemaDb ReferenceSchema => (_dbContextResolver.Resolve<ReferenceSchemaDb>() as ReferenceSchemaDb)!;
    public SecuritiesSchemaDb SecuritiesSchema => (_dbContextResolver.Resolve<SecuritiesSchemaDb>() as SecuritiesSchemaDb)!;
    public TradeSchemaDb TradeSchema => (_dbContextResolver.Resolve<TradeSchemaDb>() as TradeSchemaDb)!;
    public SystemAdminSchemaDb SystemAdminSchema => (_dbContextResolver.Resolve<SystemAdminSchemaDb>() as SystemAdminSchemaDb)!;

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
