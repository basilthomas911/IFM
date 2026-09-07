using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial interface ITradeDbContext: IObjectRepository<TradeDbContext>, ITradeDbReadContext, ITradeDbWriteContext
{
    ITradeDbReadContext DbReader { get; }
    ITradeDbWriteContext DbWriter { get; }
}
