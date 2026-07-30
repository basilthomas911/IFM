using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public interface IMarketDataDbContext : IObjectRepository<MarketDataDbContext> ,IMarketDataDbReadContext, IMarketDataDbWriteContext
{
    IMarketDataDbReadContext DbReader { get; }
    IMarketDataDbWriteContext DbWriter { get; }
}
