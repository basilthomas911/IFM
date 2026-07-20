using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;

public interface IMarketDataDbContext : IObjectRepository<MarketDataDbContext> ,IMarketDataDbReadContext, IMarketDataDbWriteContext
{
    IMarketDataDbReadContext DbReader { get; }
    IMarketDataDbWriteContext DbWriter { get; }
}
