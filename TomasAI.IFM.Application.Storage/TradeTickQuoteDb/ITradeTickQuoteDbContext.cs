using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Combines raw tick repository and ingestion capabilities.</summary>
public interface ITradeTickQuoteDbContext : IObjectRepository<TradeTickQuoteDbContext>, ITradeTickQuoteDbWriteContext, ITradeTickQuoteDbReadContext
{
    ITradeTickQuoteDbReadContext DbReader { get; }
    ITradeTickQuoteDbWriteContext DbWriter { get; }
}
