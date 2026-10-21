using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;

namespace TomasAI.IFM.Application.Storage;

public interface IDbContextFactory
{
    IEventSourceActorDbContext ActorEventSourceDb { get; }
    IObjectRepository<SequenceIdDbContext> SequenceIdDb { get; }

    IMarketDataDbContext MarketDataDb { get; }
    ITradeTickQuoteDbContext TradeTickQuoteDb { get; }
    IReferenceDbContext ReferenceDb { get; }
    ISecuritiesDbContext SecuritiesDb { get; }
    ITradeDbContext TradeDb { get; }
    ITradePlanDbContext TradePlanDb { get; }
    ISystemAdminDbContext SystemAdminDb { get; }
    IConfigurationDbContext ConfigurationDb { get; }
    IPortfolioDbContext PortfolioDb { get; }
    IMarketDataServiceDbContext MarketDataServiceDb { get; }
    /// <summary>Typed scheduled-task reads and mutations.</summary>
    IScheduledTaskDbContext ScheduledTaskDb { get; }
    /// <summary>Scheduled-task schema creation and indexes.</summary>
    ScheduledTaskSchemaDb ScheduledTaskSchema { get; }

    EventSourceSchemaDb EventSourceSchema { get; }
    SequenceIdSchemaDb SequenceIdSchema { get; }
    MarketDataSchemaDb MarketDataSchema { get; }
    TradeTickQuoteSchemaDb TradeTickQuoteSchema { get; }
    ReferenceSchemaDb ReferenceSchema { get; }
    SecuritiesSchemaDb SecuritiesSchema { get; }
    TradeSchemaDb TradeSchema { get; }
    TradePlanSchemaDb TradePlanSchema { get; }
    SystemAdminSchemaDb SystemAdminSchema { get; }
    ConfigurationSchemaDb ConfigurationSchema { get; }
    PortfolioSchemaDb PortfolioSchema { get; }
    MarketDataServiceSchemaDb MarketDataServiceSchema { get; }

}
