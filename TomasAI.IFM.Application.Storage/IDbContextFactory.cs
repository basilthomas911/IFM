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
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
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

public interface IDbContextFactory
{
    IObjectRepository<TRepo> Get<TRepo>() where TRepo : IObjectRepository;
    IDbContextPool<ReferenceDbContext> ReferencePool { get; }

    IObjectRepository<EventSourceActorDbContext> ActorEventSourceDb { get; }
    IObjectRepository<LogDbContext> LogDb { get; }
    IObjectRepository<SequenceIdDbContext> SequenceIdDb { get; }

    IFundDbContext FundDb { get; }

    //IObjectRepository<MarketDataDbContext> MarketDataDb { get; }
    IMarketDataDbContext MarketDataDb { get; }
    IOptionPricerDbContext OptionPricerDb { get; }
    IObjectRepository<PredictiveModelDbContext> PredictiveModelDb { get; }
    IReferenceDbContext ReferenceDb { get; }
    ISecuritiesDbContext SecuritiesDb { get; }
    ITradeDbContext TradeDb { get; }
    IObjectRepository<EconomicCalendarsDbContext> EconomicCalendarsDb { get; }
    IYieldCurveRatesDbContext YieldCurveRatesDb { get; }
    ISystemAdminDbContext SystemAdminDb { get; }

    EventSourceSchemaDb EventSourceSchema { get; }
    LogSchemaDb LogSchema { get; }
    SequenceIdSchemaDb SequenceIdSchema { get; }
    FundSchemaDb FundSchema { get; }
    MarketDataSchemaDb MarketDataSchema { get; }
    OptionPricerSchemaDb OptionPricerSchema { get; }
    PredictiveModelSchemaDb PredictiveModelSchema { get; }
    ReferenceSchemaDb ReferenceSchema { get; }
    SecuritiesSchemaDb SecuritiesSchema { get; }
    TradeSchemaDb TradeSchema { get; }
    SystemAdminSchemaDb SystemAdminSchema { get; }

}
