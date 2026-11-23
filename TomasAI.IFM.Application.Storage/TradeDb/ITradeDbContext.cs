using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
/// <summary>The generic Trade repository with read and write capabilities.</summary>
public interface ITradeDbContext : IObjectRepository<TradeDbContext>, ITradeDbReadContext, ITradeDbWriteContext,
    TomasAI.IFM.Application.MarketData.Recovery.IOpenPositionEndOfDayStore,
    TomasAI.IFM.Application.MarketData.Recovery.IPositionEndOfDayRecoveryStore
{
    /// <summary>Gets the typed read capability.</summary>
    ITradeDbReadContext DbReader { get; }

    /// <summary>Gets the typed mutation and enlisted projection capability.</summary>
    ITradeDbWriteContext DbWriter { get; }
}
