using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;


namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Combines raw tick repository and ingestion capabilities.</summary>
public interface ITradeTickQuoteDbContext : IObjectRepository<TradeTickQuoteDbContext>, ITradeTickQuoteDbWriteContext, ITradeTickQuoteDbReadContext
{
    /// <summary>Gets the typed raw tick query capability.</summary>
    ITradeTickQuoteDbReadContext DbReader { get; }
    /// <summary>Gets the typed direct-ingestion mutation capability.</summary>
    ITradeTickQuoteDbWriteContext DbWriter { get; }
}
