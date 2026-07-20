using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

public interface ITradeDbContext: IObjectRepository<TradeDbContext>, ITradeDbReadContext, ITradeDbWriteContext
{
    ITradeDbReadContext DbReader { get; }
    ITradeDbWriteContext DbWriter { get; }
}
