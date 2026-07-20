
namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Represents a collection of fund order trades that can be accessed by trade identifier and supports enumeration,
/// addition, and removal of trades.
/// </summary>
/// <remarks>This interface provides methods to manage a set of fund order trades, including checking for the
/// existence of a trade by its identifier and adding or removing trades from the collection. Implementations are
/// expected to provide efficient access by trade ID. The collection is read-only with respect to enumeration, but
/// supports modification through its methods.</remarks>
public interface IFundOrderTradeCollection : IEnumerable<IFundOrderTrade>
{
    int Count { get; }
    IFundOrderTrade? this[int tradeId] { get; }
    bool Exists(int tradeId);
    void Add(IFundOrderTrade trade);
    void AddRange(IEnumerable<IFundOrderTrade> trade);
    void Remove(IFundOrderTrade trade);
}
