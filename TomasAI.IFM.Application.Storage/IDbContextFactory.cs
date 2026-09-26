using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
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

    //IObjectRepository<MarketDataDbContext> MarketDataDb { get; }
    IMarketDataDbContext MarketDataDb { get; }
    IOptionPricerDbContext OptionPricerDb { get; }
    IReferenceDbContext ReferenceDb { get; }
    ISecuritiesDbContext SecuritiesDb { get; }
    ITradeDbContext TradeDb { get; }
    ITradePlanDbContext TradePlanDb { get; }
    ISystemAdminDbContext SystemAdminDb { get; }
    IConfigurationDbContext ConfigurationDb { get; }
    IPortfolioDbContext PortfolioDb { get; }
    IMarketDataServiceDbContext MarketDataServiceDb { get; }

    EventSourceSchemaDb EventSourceSchema { get; }
    SequenceIdSchemaDb SequenceIdSchema { get; }
    MarketDataSchemaDb MarketDataSchema { get; }
    OptionPricerSchemaDb OptionPricerSchema { get; }
    ReferenceSchemaDb ReferenceSchema { get; }
    SecuritiesSchemaDb SecuritiesSchema { get; }
    TradeSchemaDb TradeSchema { get; }
    TradePlanSchemaDb TradePlanSchema { get; }
    SystemAdminSchemaDb SystemAdminSchema { get; }
    ConfigurationSchemaDb ConfigurationSchema { get; }
    PortfolioSchemaDb PortfolioSchema { get; }
    MarketDataServiceSchemaDb MarketDataServiceSchema { get; }

}
