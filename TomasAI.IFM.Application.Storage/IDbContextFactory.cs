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

public interface IDbContextFactory
{
    IObjectRepository<TRepo> Get<TRepo>() where TRepo : IObjectRepository;
    IDbContextPool<ReferenceDbContext> ReferencePool { get; }

    IObjectRepository<EventSourceDbContext> EventSourceDb { get; }
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

}
